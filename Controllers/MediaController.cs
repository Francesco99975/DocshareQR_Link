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
                new Record("jpg,jpeg", "ff,d8,ff,db"),
                new Record("png", "89,50,4e,47,0d,0a,1a,0a"),
                new Record("pdf", "25 50 44 46"),
                new Record("gif", "47 49 46 38 39 61"),
                new Record("odt docx xlsx", "50 4B 03 04"),
                new Record("txt", "0E FE FF")
            };
            sniffer.Populate(supportedFiles);

            var formCollection = await Request.ReadFormAsync();
            var files = formCollection.Files;

            if (files.Count <= 0) return BadRequest();

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
                if (file.Length > 500000) return BadRequest("File too large");

                byte[] fileHead = ReadFileHead(file);
                var results = sniffer.Match(fileHead);
                if (results.Count <= 0) return BadRequest("Cannot upload this type of file");
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

            return BadRequest("Could not create group");
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteGroup(string id)
        {
            _docGroupRepository.RemoveGroup(await _docGroupRepository.GetGroup(id));

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
            return url.Contains("image");
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