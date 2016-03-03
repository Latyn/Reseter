using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reseter.RequestManager
{
    public class OpinionDocument
    {
        public OpinionDocument(int _id, string _sourcefile, string docketNumber) {
            Id = _id;
            SourceFile = _sourcefile;
            Docket = docketNumber;
        }

        public int Id { set; get; }

        public string SourceFile { get; set; }


        public string Docket { get; set; }
    }
}
