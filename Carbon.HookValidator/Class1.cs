using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carbon.HookValidator
{
    public class Class1
    {
        public void OnDisconnected ( string strReason, Network.Connection connection, ref ServerMgr __instance )
        {
            __instance.connectionQueue.RemoveConnection ( connection );
            ConnectionAuth.OnDisconnect ( connection );
            PlatformService.Instance.EndPlayerSession ( connection.userid );
            EACServer.OnLeaveGame ( connection );
            BasePlayer basePlayer = connection.player as BasePlayer;
            if ( ( bool )basePlayer )
            {
                basePlayer.OnDisconnected ();
            }
        }
    }
}
