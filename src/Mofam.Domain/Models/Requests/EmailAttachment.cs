namespace Mofam.Domain.Models.Requests;

// A file to attach to an email (any media type). EmailHelper attaches it, so any form that sends an email can use it.
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
