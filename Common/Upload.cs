using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FaceAPI.Common
{
    public class Upload
    {
        string folderName = Path.Combine("Data");
        public string GetPathName(string folder)
        {

            var pathCurrent = Path.Combine(Directory.GetCurrentDirectory(), folderName, folder);

            if (!Directory.Exists(pathCurrent))
            {
                DirectoryInfo directory = Directory.CreateDirectory(pathCurrent);
            }

            return pathCurrent;
        }

        public void UploadFile(IFormFile file, string fileName, string folder)
        {
            var pathToSave = Path.Combine(this.GetPathName(folder));

            var fullPath = Path.Combine(pathToSave, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }
        }

        public void DeleteFile(string fileName, string folder)
        {
            if (File.Exists(Path.Combine(this.GetPathName(folder), fileName)))
            {
                File.Delete(Path.Combine(this.GetPathName(folder), fileName));
            }
        }
    }
}
