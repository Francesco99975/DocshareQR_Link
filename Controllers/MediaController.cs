using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CloudinaryDotNet.Actions;
using docshareqr_link.DTOs;
using docshareqr_link.Entities;
using docshareqr_link.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Myrmec;
using platterr_api.Controllers;

namespace docshareqr_link.Controllers
{
    public class MediaController : BaseApiController
    {
        private readonly IDocFileService _docFileService;
        private readonly IDocGroupRepository _docGroupRepository;
        private string _Host;
        public MediaController(IDocFileService docFileService, IDocGroupRepository docGroupRepository, IHttpContextAccessor context)
        {
            var request = context.HttpContext.Request;
            _Host = $"{request.Scheme}://{request.Host}";
            _docGroupRepository = docGroupRepository;
            _docFileService = docFileService;
        }

        [HttpGet("{devId}")]
        public async Task<ActionResult<List<DocGroupDto>>> GetDocs(string devId)
        {
            var groups = await _docGroupRepository.GetGroups(devId);

            return Ok(groups.Select(x => new DocGroupDto
            {
                Id = x.Id,
                Name = x.Name,
                Url = _Host + "/" + x.Id,
                CreatedAt = x.CreatedAt
            }).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<DocGroupDto>> AddGroup()
        {
            var sniffer = new Sniffer();
            var supportedFiles = new List<Record>
            {
                new Record("doc xls ppt msg", "D0 CF 11 E0 A1 B1 1A E1"),
                new Record("jpg jpeg", "FF D8 FF DB"),
                new Record("jpg jpeg", "FF D8 FF E0 00 10 4A 46 49 46 00 01"),
                new Record("jpg jpeg", "FF D8 FF EE"),
                new Record("jpg jpeg", "FF D8 FF E1 ?? ?? 45 78 69 66 00 00"),
                new Record("png", "89 50 4E 47 0D 0A 1A 0A"),
                new Record("pdf", "25 50 44 46"),
                new Record("gif", "47 49 46 38 39 61"),
                new Record("odt docx xlsx pptx", "50 4B 03 04"),
                new Record("txt", "EF BB BF"),
                new Record("txt", "FF FE"),
                new Record("txt", "FF FE 00 00"),
                new Record("txt", "00 00 FF FE"),
                new Record("txt", "0E FE FF"),
                new Record("mp3", "FF FB"),
                new Record("mp3", "FF F3"),
                new Record("mp3", "FF F2"),
                new Record("mp3", "49 44 33")
            };
            sniffer.Populate(supportedFiles);

            var formCollection = await Request.ReadFormAsync();
            var files = formCollection.Files;

            if (files.Count <= 0) return BadRequest();

            if (files.Aggregate(0L, (x, y) => x + y.Length) > 1000000) return BadRequest("Files too large. Upload less files");

            var group = new DocGroup
            {
                Id = Guid.NewGuid().ToString(),
                Name = formCollection["name"],
                DeviceId = formCollection["deviceId"],
                CreatedAt = DateTime.UtcNow,
            };

            var password = String.Concat(formCollection["password"].ToString().Where(c => !Char.IsWhiteSpace(c)));

            if (password.Length > 0)
            {
                using var hmac = new HMACSHA512();
                group.PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                group.PasswordSalt = hmac.Key;
            }

            List<DocFile> dataFiles = new List<DocFile>();

            foreach (var file in files)
            {
                // if (file.Length > 500000) return BadRequest("File too large");

                byte[] fileHead = ReadFileHead(file);
                var results = sniffer.Match(fileHead);
                if (results.Count <= 0) return BadRequest("Cannot upload this type of file: " + file.FileName);
                var result = await _docFileService.AddFileAsync(file);
                if (result.Error != null) return BadRequest(result.Error.Message);

                var docFile = new DocFile
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Name = file.Name,
                    PublicId = result.PublicId,
                    Url = ValidateUrl(result.SecureUrl.AbsoluteUri) ? GetDownloadUrl(result.SecureUrl.AbsoluteUri) : result.SecureUrl.AbsoluteUri,
                    Size = file.Length,
                    Group = group,
                    GroupId = group.Id
                };

                dataFiles.Add(docFile);
            }

            group.Files = dataFiles;

            _docGroupRepository.AddGroup(group);

            if (await _docGroupRepository.SaveAllAsync())
            {
                return Ok(new DocGroupDto
                {
                    Id = group.Id,
                    Name = group.Name,
                    Url = _Host + "/" + group.Id,
                    CreatedAt = group.CreatedAt
                });
            }

            return BadRequest("Could not create Docshare QR");
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteGroup(string id)
        {
            await _docGroupRepository.RemoveGroup(await _docGroupRepository.GetGroup(id));

            if (await _docGroupRepository.SaveAllAsync())
            {
                return Ok();
            }

            return BadRequest();
        }

        private static byte[] ReadFileHead(IFormFile file)
        {
            using var fs = new BinaryReader(file.OpenReadStream());
            var bytes = new byte[20];
            fs.Read(bytes, 0, 20);
            return bytes;
        }

        private static bool ValidateUrl(string url)
        {
            return url.Contains("image") || url.Contains("video");
        }

        private static string GetDownloadUrl(string url)
        {
            var src = "upload/";
            var index = url.LastIndexOf(src);
            index = index + src.Length;
            var list = new List<char>(url);
            list.InsertRange(index, "fl_attachment/");

            var downloadUrl = new string(list.ToArray());

            if (url.Contains(".pdf"))
            {
                downloadUrl = downloadUrl.Replace(".pdf", ".png");
            }

            return downloadUrl;
        }
    }
}