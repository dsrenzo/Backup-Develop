using ProgramaControl.Models;
using ProgramaControl.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramaControl.Services.Ejecutores
{
    public static class RestoreExecutor
    {
        public static void Ejecutar(Operacion op)
        {
            if (string.IsNullOrWhiteSpace(op.Origen))
                throw new ArgumentException("Campo 'origen' vacío en operación restore");

            if (string.IsNullOrWhiteSpace(op.Destino))
                throw new ArgumentException("Campo 'destino' vacío en operación restore");

            if (!File.Exists(op.Origen))
                throw new FileNotFoundException($"Imagen .wim no encontrada: {op.Origen}");

            if (!Directory.Exists(op.Destino))
                Directory.CreateDirectory(op.Destino);

            Logger.Log($"[INFO] Restaurando imagen: {op.Origen}");

            string argumentos = $"/Apply-Image /ImageFile:\"{op.Origen}\" /Index:1 /ApplyDir:\"{op.Destino}\"";

            var proceso = Process.Start(new ProcessStartInfo("dism", argumentos)
            {
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            });

            proceso.WaitForExit();

            if (proceso.ExitCode != 0)
                throw new Exception($"Error al aplicar imagen. Código: {proceso.ExitCode}");

            Logger.Log("[OK] Imagen restaurada correctamente.");
        }

        private static long CalcularTamanoCarpeta(string ruta)
        {
            return Directory.EnumerateFiles(ruta, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }

        private static long ObtenerEspacioDisponible(string ruta)
        {
            var drive = new DriveInfo(Path.GetPathRoot(ruta)!);
            return drive.AvailableFreeSpace;
        }

        private static void CopiarCarpeta(string origen, string destino)
        {
            foreach (string dirPath in Directory.GetDirectories(origen, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(origen, destino));
            }

            foreach (string filePath in Directory.GetFiles(origen, "*", SearchOption.AllDirectories))
            {
                string destinoArchivo = filePath.Replace(origen, destino);
                File.Copy(filePath, destinoArchivo, overwrite: true);
            }
        }
    }
}
