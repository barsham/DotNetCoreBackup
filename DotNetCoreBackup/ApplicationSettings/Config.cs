using System.Runtime.Serialization;

namespace DotNetCoreBackup.ApplicationSettings
{
    [DataContract]
    public class Config
    {
        [DataMember] public BackupParameter[] BackupParameterCollection { get; set; }
    }


    [DataContract]
    public class BackupParameter
    {
        [DataMember] public string Source { get; set; }

        [DataMember] public string TempFolder { get; set; }

        [DataMember] public string Destination { get; set; }
    }
}