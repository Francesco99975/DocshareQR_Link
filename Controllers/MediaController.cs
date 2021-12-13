using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CloudinaryDotNet.Actions;
using docshareqr_link.DTOs;
using docshareqr_link.Entities;
using docshareqr_link.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Myrmec;
using Newtonsoft.Json;
using platterr_api.Controllers;

namespace docshareqr_link.Controllers
{
    class Bitly
    {
        public string link { get; set; }
    }
    public class MediaController : BaseApiController
    {
        private readonly IDocFileService _docFileService;
        private readonly IDocGroupRepository _docGroupRepository;
        private string _Host;
        private Sniffer _Sniffer;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        public MediaController(IDocFileService docFileService, IDocGroupRepository docGroupRepository, IHttpContextAccessor context, IWebHostEnvironment env, IConfiguration config)
        {
            _config = config;
            _env = env;
            var request = context.HttpContext.Request;
            _Host = $"{request.Scheme}://{request.Host}";
            _docGroupRepository = docGroupRepository;
            _docFileService = docFileService;
            _Sniffer = new Sniffer();
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
                new Record("mp3", "49 44 33"),
                new Record("webp", "52 49 46 46 ?? ?? ?? ?? 57 45 42 50")
            };
            _Sniffer.Populate(supportedFiles);
        }

        [HttpGet("{devId}")]
        public async Task<ActionResult<List<DocGroupDto>>> GetDocs(string devId)
        {
            var groups = await _docGroupRepository.GetGroups(devId);

            return Ok(groups.Select(async (x) =>
            {
                var url = await getGroupUrl(_env.IsProduction(), _config.GetSection("BITLY_KEY").ToString(), _Host, x);
                return new DocGroupDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Url = url,
                    CreatedAt = x.CreatedAt
                };
            }).ToList());
        }

        [HttpGet]
        public async Task<PhysicalFileResult> DownloadFile([FromQuery] string group, string name)
        {
            var file = await _docGroupRepository.GetFile(group, name);

            return PhysicalFile(Path.Combine(Environment.CurrentDirectory + "/media", name), file.ContentType);
        }

        [HttpPost]
        public async Task<ActionResult<DocGroupDto>> AddGroup()
        {
            var formCollection = await Request.ReadFormAsync();
            var files = formCollection.Files;

            if (files.Count <= 0) return BadRequest();

            if (files.Aggregate(0L, (x, y) => x + y.Length) > 1000000) return BadRequest("Files too large. Upload less files");

            if (await _docGroupRepository.Overloaded(formCollection["deviceId"])) return BadRequest("You alredy create a maximum of 10 QR codes");

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
                var results = _Sniffer.Match(fileHead);
                if (results.Count <= 0) return BadRequest("Cannot upload this type of file: " + file.FileName);

                //Cloudinary Implementaion
                // var result = await _docFileService.AddFileAsync(file);
                // if (result.Error != null) return BadRequest("Upload Error:" + result.Error.Message);

                var randomFilename = Path.GetRandomFileName();
                var filePath = Path.Combine(Environment.CurrentDirectory + "/media", randomFilename);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                var docFile = new DocFile
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Name = file.Name,
                    // PublicId = result.PublicId, <-- Cloudinary ID
                    PublicId = randomFilename,
                    // Url = ValidateUrl(result.SecureUrl.AbsoluteUri) 
                    //             ? GetDownloadUrl(result.SecureUrl.AbsoluteUri) 
                    //             : result.SecureUrl.AbsoluteUri, <-- Cloudinary URL
                    Url = _Host + "/media?group=" + group.Id + "&name=" + randomFilename,
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
                var url = "";
                try
                {
                    url = await getGroupUrl(_env.IsProduction(), _config.GetSection("BITLY_KEY").ToString(), _Host, group);
                }
                catch (System.Exception)
                {
                    return BadRequest("Could not generate Url");
                }

                return Ok(new DocGroupDto
                {
                    Id = group.Id,
                    Name = group.Name,
                    Url = url,
                    CreatedAt = group.CreatedAt
                });
            }

            return BadRequest("Could not create Docshare QR");
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

        private static async Task<string> getGroupUrl(bool prod, string key, string host, DocGroup group)
        {
            var url = "";
            if (prod)
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + key);
                var content = new StringContent(
                            JsonConvert.SerializeObject(new
                            {
                                long_url = host + "/" + group.Id
                            }),
                            Encoding.UTF8,
                            "application/json"
                );
                var res = await client.PostAsync("https://api-ssl.bitly.com/v4/shorten", content);
                var body = JsonConvert.DeserializeObject<Bitly>(await res.Content.ReadAsStringAsync());
                url = body.link;
            }
            else
            {
                url = host + "/" + group.Id;
            }

            return url;
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