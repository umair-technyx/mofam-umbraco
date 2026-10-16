namespace Mofam.CMS.Security;

// The Microsoft account signed in, but it has no App Role for an Umbraco group.
// It lets the failure handler show the right message for this case.
public sealed class EntraNoRoleException(string message) : Exception(message);
