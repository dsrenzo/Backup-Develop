using ProgramaControl.Models;
using ProgramaControl.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramaControl.Services.Ejecutores
{
    public static class DeleteExecutor
    {
        public static void Ejecutar(Operacion op)
        {
            if (string.IsNullOrWhiteSpace(op.Destino))
                throw new ArgumentException("Campo 'destino' vacío en operación delete");

            Logger.Log($"[INFO] Eliminando: {op.Destino}");

            try
            {
                if (Directory.Exists(op.Destino))
                {
                    Directory.Delete(op.Destino, recursive: true);
                    Logger.Log("[OK] Directorio eliminado.");
                }
                else if (File.Exists(op.Destino))
                {
                    File.Delete(op.Destino);
                    Logger.Log("[OK] Archivo eliminado.");
                }
                else
                {
                    Logger.Log("[WARN] Ruta no encontrada.");
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Error al eliminar: {ex.Message}");
            }
        }
    }
}
