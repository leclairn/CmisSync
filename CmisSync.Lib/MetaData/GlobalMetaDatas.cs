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
            GlobalMetaDatas metadatas;
            using (StreamReader reader = new StreamReader(path))
            {
                metadatas = (GlobalMetaDatas)serializer.Deserialize(reader);
                reader.Close();
            }
            return metadatas;
        }

        /// <summary>
        /// Save metadatas to Xml File
        /// </summary>
        /// <param name="path">path to the Xml file </param>
        public void Save(string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GlobalMetaDatas));
            using (FileStream writer = File.Create(path))
            {
                serializer.Serialize(writer, this);
                writer.Close();
            }
        }
    }

}
