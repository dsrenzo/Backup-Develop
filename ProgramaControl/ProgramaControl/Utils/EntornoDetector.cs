using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramaControl.Utils
{
    public static class EntornoDetector
    {
        public static string Detectar()
        {
            if (OperatingSystem.IsWindows())
            {
                Console.WriteLine("[INFO] Entorno detectado: Windows");
                return "windows";
            }
            else if (OperatingSystem.IsLinux())
            {
                Console.WriteLine("[INFO] Entorno detectado: Linux");
                return "linux";
            }
            else
            {
                Console.WriteLine("[ERROR] Entorno no soportado");
                throw new NotSupportedException("Sistema operativo no compatible");
            }
        }
    }
}
