using UnityEngine;

namespace Doxfen.Systems.AI
{
    public class LogsHandler
    {
        public static void Log(string message, Object context = null)
        {
            if (context == null)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.Log(message, context);
            }
        }
    }
}
