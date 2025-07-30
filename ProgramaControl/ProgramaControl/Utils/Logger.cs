using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramaControl.Utils
{
    public static class Logger
    {
        private static readonly string logPath = @"C:\OperacionesPendientes\ProgramaControl.log";

        public static void Log(string mensaje)
        {
            try
            {
                var linea = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensaje}";
                File.AppendAllText(logPath, linea + Environment.NewLine);
                Console.WriteLine(linea);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] No se pudo escribir el log: {ex.Message}");
            }
        }
    }
}
