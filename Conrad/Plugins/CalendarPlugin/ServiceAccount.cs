// ReSharper disable InconsistentNaming
namespace CalendarPlugin;

[Serializable]
internal class ServiceAccount
{
    public string Type { get; set; } = string.Empty;
    public string Project_Id { get; set; } = string.Empty;
    public string Private_Key_Id { get; set; } = string.Empty;
    public string Private_Key { get; set; } = string.Empty;
    public string Client_Email { get; set; } = string.Empty;
    public string Client_Id { get; set; } = string.Empty;
    public string Auth_Uri { get; set; } = string.Empty;
    public string Token_Uri { get; set; } = string.Empty;
    public string Auth_Provider_X509_Cert_Url { get; set; } = string.Empty;
    public string Client_X509_Cert_Url { get; set; } = string.Empty;
    public string Universe_Domain { get; set; } = string.Empty;
}
