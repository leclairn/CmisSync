using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace CmisSync.Lib.MetaData
{
    public class GlobalMetaDatas
    {
        public string typename;
        public List<string> aspects;
        public MetaDatas metadatas;


        public GlobalMetaDatas()
        {
            typename = "";
            aspects = new List<string>();
            metadatas = new MetaDatas();
        }

        /// <summary>
        /// Load metadatas from Xml File
        /// </summary>
        /// <param name="path">path to the Xml file </param>
        public static GlobalMetaDatas Load(string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GlobalMetaDatas));
            StreamReader reader = new StreamReader(path);
            GlobalMetaDatas metadatas = (GlobalMetaDatas)serializer.Deserialize(reader);
            reader.Close();
            return metadatas;
        }

        /// <summary>
        /// Save metadatas to Xml File
        /// </summary>
        /// <param name="path">path to the Xml file </param>
        public void Save(string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GlobalMetaDatas));
            StreamWriter writer = new StreamWriter(path);
            serializer.Serialize(writer, this);
            writer.Close();
        }
    }

}
