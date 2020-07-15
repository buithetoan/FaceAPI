using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FaceAPI.Common;
using FaceAPI.Common.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

namespace FaceAPI.Controllers
{
    [ApiController]
    public class IdentifyFaceController : ControllerBase
    {
        private readonly string endPoint = SubscriptionKey.ENDPOINT;
        private readonly string key = SubscriptionKey.KEY;

        [HttpGet("identifyFace/persons")]
        public async Task<ActionResult> GetPersons([FromQuery] string personGroupId)
        {
            try
            {
                var faceServiceClient = new FaceServiceClient(key, endPoint);
                var persons = await faceServiceClient.GetPersonsAsync(personGroupId);
                if (persons.Length > 0) return StatusCode(200, persons);
                return StatusCode(200, new {Flag = "No one on the list!" });

            }
            catch (Exception)
            {
                return StatusCode(404, new { Flag = "Not found person group!" });
            }

        }

        [HttpPost("identifyFace")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> IdentifyFace([FromForm] string personGroupId)
        {
            try
            {
                var faceServiceClient = new FaceServiceClient(key, endPoint);
                //string testImageFile = @"D:\MyFace\FaceAPI\FaceAPI\Data\Face\WIN_20200702_14_38_29_Pro.jpg";
                List<EmployeeResult> dataResults = new List<EmployeeResult>();
                if (Request.Form.Files.Count != 0)
                {
                    IFormFile file = Request.Form.Files[0];

                    if (file.Length > 0)
                    {
                        Upload uploadFile = new Upload();

                        string fileName = DateTime.Now.ToString("yyyyMMdd'T'HHmm") + "-" + file.FileName;

                        uploadFile.UploadFile(file, fileName, "Pictures");

                        var fullPath = Path.Combine(Path.Combine(uploadFile.GetPathName("Pictures"), fileName));

                        using (Stream s = System.IO.File.OpenRead(fullPath))
                        {
                            var faces = await faceServiceClient.DetectAsync(s);
                            if (faces.Length >0)
                            {
                                var faceIds = faces.Select(x => x.FaceId).ToArray();
                                var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);

                                foreach (var identifyResult in results)
                                {
                                    if (identifyResult.Candidates.Length == 0)
                                    {
                                        Console.WriteLine("No one identified");
                                        uploadFile.DeleteFile(file.FileName, "Pictures");

                                    }
                                    else if (identifyResult.Candidates[0].Confidence > 0.6)
                                    {
                                        Console.WriteLine("Result of face: {0}", identifyResult.FaceId);
                                        Console.WriteLine("Confidence: {0}", identifyResult.Candidates[0].Confidence);
                                        // Get top 1 among all candidates returned
                                        var candidateId = identifyResult.Candidates[0].PersonId;
                                        //var person = await faceClient.PersonGroupPerson.GetAsync(personGroupId,candidateId);
                                        var person = await faceServiceClient.GetPersonAsync(personGroupId, candidateId);
                                        Console.WriteLine("Identified as {0}", person.Name);
                                        dataResults.Add(new EmployeeResult
                                        {
                                            Confidence = (identifyResult.Candidates[0].Confidence * 100).ToString() + "%",
                                            TimeLog = DateTime.Now,
                                            EmployeeNo = person.Name,
                                        });
                                    }
                                    else
                                    {
                                        uploadFile.DeleteFile(file.FileName, "Pictures");
                                    }
                                }
                            }
                            else
                            {
                                uploadFile.DeleteFile(file.FileName, "Pictures");
                                return StatusCode(200, new { Flag = "Unconfirmed faces. Please try again!" });
                            }                            
                        }
                    }
                }
                if (dataResults.Count > 0) return StatusCode(200, dataResults);
                return StatusCode(200, new { Flag = "Unconfirmed faces. Please try again!" });
            }
            catch (Exception)
            {
                return StatusCode(200, new { Flag = "Unconfirmed faces. Please try again!" });
            }
        }

        [HttpPost("identifyFace/createPersonGroup")]
        public async Task<ActionResult> CreatePersonGroup([FromBody] PersonGroupRequest personGroupRequest)
        {
            try
            {
                var faceServiceClient = new FaceServiceClient(key, endPoint);
                string personGroupId = personGroupRequest.PersonGroupId;
                bool personGroupExists = false;
                try
                {
                    var personGroups = await faceServiceClient.GetPersonGroupsAsync();

                    personGroupExists = personGroups.Any(x => x.PersonGroupId == personGroupId);
                }
                catch (Exception)
                {
                    throw;
                }
                if (personGroupExists)
                {
                    await faceServiceClient.DeletePersonGroupAsync(personGroupId);
                }
                await faceServiceClient.CreatePersonGroupAsync(personGroupId, personGroupRequest.Name, personGroupRequest.UserData);

                return StatusCode(200, new { Flag = "Person Group created!" });
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("identifyFace/trainPersonGroup")]
        public async Task<ActionResult> TrainPersonGroup([FromQuery] string personGroupId)
        {
            var faceServiceClient = new FaceServiceClient(key, endPoint);
            bool personGroupExists = false;
            try
            {
                await faceServiceClient.GetPersonGroupAsync(personGroupId);
                personGroupExists = true;
            }
            catch (Exception)
            {
                throw;
            }
            if (personGroupExists)
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "PersonGroup");
                var imageList =
                            new ConcurrentBag<string>(
                                Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                                    .Where(s => s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".bmp") || s.ToLower().EndsWith(".gif")));
                int count = 0;
                foreach (var img in imageList)
                {
                    using (Stream pathImage = System.IO.File.OpenRead(img))
                    {
                        Thread.Sleep(6000);
                        FileStream fs = pathImage as FileStream;
                        string employeeName = Path.GetFileName(fs.Name).Split(".")[0];
                        var employee = await faceServiceClient.CreatePersonAsync(personGroupId, employeeName);
                        await faceServiceClient.AddPersonFaceAsync(personGroupId, employee.PersonId, fs);
                        Console.WriteLine($"Name: {employeeName} - count: {count}");
                        count++;
                    }
                }
                Thread.Sleep(6000);
                await faceServiceClient.TrainPersonGroupAsync(personGroupId);
                TrainingStatus trainingStatus = null;
                while (true)
                {
                    trainingStatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);
                    if (trainingStatus.Status != Status.Running)
                    {
                        break;
                    }
                    await Task.Delay(1000);
                }
                return Ok();
            }
            else
            {
                Console.WriteLine("Not found person group");
                return StatusCode(404, $"Not found person group: {personGroupId}");
            }
        }

        [HttpPut("identifyFace/updatePersonGroup")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UpdatePersonGroup([FromForm] string personGroupId, [FromForm] string employeeNo)
        {
            if (Request.Form.Files.Count != 0)
            {
                IFormFile file = Request.Form.Files[0];

                if (file.Length > 0)
                {
                    Upload uploadFile = new Upload();
                    string fileName = employeeNo + ".jpg";
                    uploadFile.UploadFile(file, fileName, "PersonGroup");
                    var fullPath = Path.Combine(Path.Combine(uploadFile.GetPathName("PersonGroup"), fileName));

                    var faceServiceClient = new FaceServiceClient(key, endPoint);
                    bool personGroupExists = false;
                    try
                    {
                        await faceServiceClient.GetPersonGroupAsync(personGroupId);
                        personGroupExists = true;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    if (personGroupExists)
                    {
                        using (Stream pathImage = System.IO.File.OpenRead(fullPath))
                        {
                            Thread.Sleep(6000);
                            FileStream fs = pathImage as FileStream;
                            string employeeName = Path.GetFileName(fs.Name).Split(".")[0];
                            var employeeCreate = await faceServiceClient.CreatePersonAsync(personGroupId, employeeName);
                            await faceServiceClient.AddPersonFaceAsync(personGroupId, employeeCreate.PersonId, fs);
                        }
                    }
                    Thread.Sleep(6000);
                    await faceServiceClient.TrainPersonGroupAsync(personGroupId);
                    TrainingStatus trainingStatus = null;
                    while (true)
                    {
                        trainingStatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);
                        if (trainingStatus.Status != Status.Running)
                        {
                            break;
                        }
                        await Task.Delay(1000);
                    }
                }
                else return StatusCode(404, new { Flag = "No photos found!" });
            }
            return StatusCode(200, new { Flag = "Update successful!" });
        }
    }
}