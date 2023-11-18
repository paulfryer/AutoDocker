using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithyParser.Models.Traits
{
    public class HttpTrait
    {
        public string Method { get; set; }

        public string Uri { get; set; }

        public int Code { get; set; }
    }
}
