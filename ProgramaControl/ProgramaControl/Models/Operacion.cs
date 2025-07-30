using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramaControl.Models
{
    public class Operacion
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Origen { get; set; } = "";
        public string Destino { get; set; } = "";
        public string Entorno { get; set; } = "";
        public string Fecha { get; set; } = "";
        public string Estado { get; set; } = "";
        public int nvolumenorigen { get; set; }
    }
}
