using System.Collections.Generic;
using System.Web;

namespace CmisSync.Lib.Conf
{
    public class Application
    {
        public string user;
        public string password;
        public List<FolderName> folders;
        public List<TypeInfos> typeInfos;
        public TypeSynchro typeSynchro;

        public Application()
        {
            user = "";
            password = "";
        }

        public Application(string user, string password, List<FolderName> folders, List<TypeInfos> typeInfos, TypeSynchro typeSynchro)
        {
            this.user = user;
            this.password = password;
            this.folders = folders;
            this.typeInfos = typeInfos;
            this.typeSynchro = typeSynchro;
        }
    }
}