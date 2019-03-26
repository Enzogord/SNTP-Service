using System;

namespace SNTPService
{
    /// <summary>
    /// Subscribe to this event to receive request messages.
    /// </summary>
    public delegate void SntpMessageEventHandler(object sender, SntpMessageEventArgs args);
}
