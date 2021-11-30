using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using docshareqr_link.Entities;

namespace docshareqr_link.Interfaces
{
    public interface IDocGroupRepository
    {
        void AddGroup(DocGroup group);
        void RemoveGroup(DocGroup group);
        Task<List<DocGroup>> GetDeprecatedGroups();
        Task<List<DocGroup>> GetGroups(string deviceId);
        Task<DocGroup> GetGroup(string groupId);
        Task<bool> Authenticate(string id, string password);
        Task<bool> SaveAllAsync();
    }
}