using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using docshareqr_link.Entities;
using docshareqr_link.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace docshareqr_link.Data
{
    public class DocGroupRepository : IDocGroupRepository
    {
        private readonly DataContext _context;
        private readonly IDocFileService _docFileService;
        public DocGroupRepository(DataContext context, IDocFileService docFileService)
        {
            _docFileService = docFileService;
            _context = context;
        }

        public void AddGroup(DocGroup group)
        {
            _context.DocGroups.Add(group);
        }

        public async Task<bool> Authenticate(string id, string password)
        {
            var group = await GetGroup(id);

            using var hmac = new HMACSHA512(group.PasswordSalt);

            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

            for (int i = 0; i < computedHash.Length; ++i)
            {
                if (computedHash[i] != group.PasswordHash[i])
                    return false;
            }

            return true;
        }

        public async Task<List<DocGroup>> GetDeprecatedGroups()
        {
            return await _context.DocGroups.Include(x => x.Files).Where(x => DateTime.UtcNow > (x.CreatedAt.AddDays(3))).ToListAsync();
        }

        public async Task<DocGroup> GetGroup(string groupId)
        {
            return await _context.DocGroups.Include(x => x.Files).Where(x => x.Id == groupId).FirstOrDefaultAsync();
        }

        public async Task<List<DocGroup>> GetGroups(string deviceId)
        {
            return await _context.DocGroups.Where(x => x.DeviceId == deviceId).ToListAsync();
        }

        public async Task RemoveGroup(DocGroup group)
        {
            foreach (var file in group.Files)
            {
                await _docFileService.DeleteFileAsync(file.PublicId);
            }
            _context.DocGroups.Remove(group);
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}