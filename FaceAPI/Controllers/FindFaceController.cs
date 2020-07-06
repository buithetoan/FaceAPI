using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FaceAPI.Common;
using FaceAPI.Common.Contants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Newtonsoft.Json;

namespace FaceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FindFaceController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult> FindFace([FromForm] string name)
        {
            try
            {
                var faceServiceClient = new FaceServiceClient(APIs.KEY, APIs.ENDPOINT);
                if (!System.IO.File.Exists(@"D:\MyFace\FaceAPI\FaceAPI\Data\dataPerson.json"))
                {
                    var path = System.IO.Path.Combine(@"D:\MyFace\FaceAPI\FaceAPI\Data\Persons\");
                    var imageList =
                                new ConcurrentBag<string>(
                                    Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                                        .Where(s => s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".bmp") || s.ToLower().EndsWith(".gif")));
                    List<Guid> faceIds = new List<Guid>();
                    List<Employee> persons = new List<Employee>();
                    int count = 0;
                    foreach (var img in imageList)
                    {
                        using (var pathImage = System.IO.File.OpenRead(img))
                        {
                            if (count > 15)
                            {
                                Thread.Sleep(61000);
                                count = 0;
                            }
                            var detectedFaces = await faceServiceClient.DetectAsync(pathImage);
                            foreach (var detectedFace in detectedFaces)
                            {
                                Console.WriteLine($"FaceId: {detectedFace.FaceId} - count: {count}");
                                faceIds.Add(detectedFace.FaceId);
                                persons.Add(new Employee
                                {
                                    FaceId = detectedFace.FaceId,
                                    NickName = Path.GetFileName(pathImage.Name).Split(".")[0],
                                });
                                count++;
                            }
                        }
                    }
                    string json = JsonConvert.SerializeObject(persons.ToArray());
                    System.IO.File.WriteAllText(@"D:\MyFace\FaceAPI\FaceAPI\Data\dataPerson.json", json);
                }
                List<EmployeeResult> dataResult = new List<EmployeeResult>();
                using (StreamReader r = new StreamReader(@"D:\MyFace\FaceAPI\FaceAPI\Data\dataPerson.json"))
                {
                    string json = r.ReadToEnd();
                    List<Employee> persons = JsonConvert.DeserializeObject<List<Employee>>(json);
                    var faceIds = persons.Select(p => p.FaceId).ToArray();
                    var picture = @"D:\MyFace\FaceAPI\FaceAPI\Data\Pictures\identification1.jpg";
                    using (var pic = System.IO.File.OpenRead(picture))
                    {
                        var faces = await faceServiceClient.DetectAsync(pic);
                        foreach (var face in faces)
                        {
                            var faceId = face.FaceId;
                            const int requestCandidatesCount = 4;
                            var result = await faceServiceClient.FindSimilarAsync(faceId, faceIds: faceIds, mode: FindSimilarMatchMode.matchPerson, maxNumOfCandidatesReturned: requestCandidatesCount);
                            if (result.Length > 0 && result.Any(p => p.Confidence >= 0.6))
                            {
                                var maxConfidence = result.OrderByDescending(x => x.Confidence).First();
                                var resultPerson = persons.Where(p => p.FaceId == maxConfidence.FaceId).FirstOrDefault();
                                dataResult.Add(new EmployeeResult
                                {
                                    FaceId = resultPerson.FaceId,
                                    NickName = resultPerson.NickName,
                                    DateTime = DateTime.Now,
                                    Confidence = maxConfidence.Confidence,
                                    EmployeeName = "",
                                    EmployeeNo = "",
                                });
                                Console.WriteLine($"Name: {resultPerson.NickName} - Confidence: { maxConfidence.Confidence}");
                                Console.WriteLine($"Time: { DateTime.Now}");
                            }
                        }
                    }
                }
                return StatusCode(200, dataResult);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}