using System.Collections.Generic;

namespace Conf
{
    public class TypeInfos
    {
        public string typename;
        public List<string> mandatoryMetadatas;

        public TypeInfos()
        {
            typename = "";
            mandatoryMetadatas = new List<string>();
        }

        public TypeInfos(string typename, List<string> mandatoryMetadatas)
        {
            this.typename = typename;
            this.mandatoryMetadatas = mandatoryMetadatas;
        }

        public TypeInfos(string typename)
        {
            this.typename = typename;
            this.mandatoryMetadatas = new List<string>();
        }
    }
}