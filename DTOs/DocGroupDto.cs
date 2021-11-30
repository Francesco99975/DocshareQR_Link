using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace docshareqr_link.DTOs
{
    public class DocGroupDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}