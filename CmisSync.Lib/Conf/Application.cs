using System.Collections.Generic;
using System.Web;

namespace Conf
{
    public class Application
    {
        public string user;
        public string password;
        public List<string> folders;
        public List<TypeInfos> typeInfos;
        public TypeSynchro typeSynchro;

        public Application()
        {
            user = "";
            password = "";
        }

        public Application(string user, string password, List<string> folders, List<TypeInfos> typeInfos, TypeSynchro typeSynchro)
        {
            this.user = user;
            this.password = password;
            this.folders = folders;
            this.typeInfos = typeInfos;
            this.typeSynchro = typeSynchro;
        }
    }
}