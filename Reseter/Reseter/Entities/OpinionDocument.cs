using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reseter.Entities
{
    public class OpinionDocument
    {
        public OpinionDocument(int _id, string _sourcefile) {
            Id = _id;
            SourceFile = _sourcefile;
        }

        public int Id { set; get; }

        public string SourceFile { get; set; }
    }
}
