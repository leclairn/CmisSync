using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Conf
{
    public class TypeExtracteurs
    {
        [XmlIgnore]
        private ContentType mimeType;

        public string name;
        public List<Extracteur> extracteurs;
        public bool forceSynchro;
        
        public TypeExtracteurs()
        {
            extracteurs = new List<Extracteur>();
        }

        public TypeExtracteurs(ContentType mimeType, List<Extracteur> extracteurs, bool forceSynchro)
        {
            this.mimeType = mimeType;
            this.extracteurs = extracteurs;
            name = ContentTypeExtensions.ToValue(mimeType);
            this.forceSynchro = forceSynchro;
        }
    }
}
