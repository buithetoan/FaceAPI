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
using Newtonsoft.Json;

namespace FaceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PersonGroupController : ControllerBase
    {
        [HttpPut]
        public async Task<ActionResult> CreatePersonGroup([FromBody] PersonGroupRequest personGroupRequest)
        {
            try
            {
                var faceServiceClient = new FaceServiceClient(APIs.KEY, APIs.ENDPOINT);

                var personGroupId = "gdit-person-group";
                var allPersonGroups = await faceServiceClient.ListPersonGroupsAsync();
                bool groupExists = false;
                if (allPersonGroups?.Any(x => x.PersonGroupId == personGroupId) == false)
                {
                    await faceServiceClient.CreatePersonGroupAsync(personGroupId, personGroupRequest.Name, personGroupRequest.UserData);
                }
                var facesIdFromPhotos = new List<Guid>();

                var path = System.IO.Path.Combine(@"D:\MyFace\FaceAPI\FaceAPI\Data\Persons\");
                var photos =
                            new ConcurrentBag<string>(
                                Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                                    .Where(s => s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".bmp") || s.ToLower().EndsWith(".gif")));

                int count = 0;
                foreach (var photo in photos)
                {
                    using (var stream = System.IO.File.OpenRead(photo))
                    {
                        if (count > 15)
                        {
                            Thread.Sleep(61000);
                            count = 0;
                        }
                        var faces = await faceServiceClient.DetectAsync(stream);
                                                
                        foreach (var face in faces)
                        {
                            Console.WriteLine($"FaceId: {face.FaceId} - count: {count}");
                            facesIdFromPhotos.Add(face.FaceId);
                            count++;
                        }
                    }
                }

                // Check similarity, with 1 face from the previous detected faces
                var similarityPerson = await faceServiceClient.IdentifyAsync(personGroupId, facesIdFromPhotos.ToArray(),4);

                Guid targetPersonId;
                if (similarityPerson[0].Candidates?.Count() > 0)
                {
                    targetPersonId = similarityPerson[0].Candidates[0].PersonId;
                }
                else
                {
                    var createdPerson = await faceServiceClient.CreatePersonAsync(personGroupId, Guid.NewGuid().ToString());
                    targetPersonId = createdPerson.PersonId;
                }

                // Add faces to Person (already existing or not)
                foreach (var photo in photos)
                {
                    await faceServiceClient.AddPersonFaceAsync(personGroupId, targetPersonId, photo);
                }

                await faceServiceClient.TrainPersonGroupAsync(personGroupId);

                return StatusCode(200);
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}