using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace docshareqr_link.Entities
{
    public class DocGroup
    {
        public string Id { get; set; }
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public ICollection<DocFile> Files { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}