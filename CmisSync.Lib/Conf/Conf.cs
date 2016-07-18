using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Conf
{
    public class Conf
    {
        public string url;
        public string repository;
        public List<Application> applications;
        public List<TypeExtracteurs> typesExtracteurs;

        public Conf()
        {
            url = "";
            repository = "";
            applications = new List<Application>();
        }

        public Conf(string url, string repository, List<Application> applications, List<TypeInfos> mandatoryMetadatas, List<TypeExtracteurs> typesExtracteurs)
        {
            this.url = url;
            this.repository = repository;
            this.applications = applications;
            this.typesExtracteurs = typesExtracteurs;
        }

        public Conf(string url, string repository)
        {
            this.url = url;
            this.repository = repository;
            this.applications = new List<Application>();
            this.typesExtracteurs = new List<TypeExtracteurs>();
        }
    }
}
