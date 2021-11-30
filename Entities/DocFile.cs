using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace docshareqr_link.Entities
{
    [Table("DocFiles")]
    public class DocFile
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Name { get; set; }
        public string PublicId { get; set; }
        public string Url { get; set; }
        public double Size { get; set; }
        public string ContentType { get; set; }
        public DocGroup Group { get; set; }
        public string GroupId { get; set; }
    }
}