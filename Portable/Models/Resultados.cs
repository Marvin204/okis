using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Web;
using static Portable.Models.MetodosNumericos;

namespace Portable.Models
{
    public class Resultados
    {
        public int IdResultado { get; set; }

        public double ResultadoFinal { get; set; }
        public int Iteraciones { get; set; }
        public double ErrorEstimado { get; set; }
        public string DetalleResultado { get; set; }
        public DateTime FechaResultado { get; set; } = DateTime.Now;

        // Relaciones
        [ForeignKey("Ecuaciones")]
        public int IdEcuacion { get; set; }
        public virtual Ecuaciones Ecuacion { get; set; }

        [ForeignKey("MetodosNumericos")]
        public int IdMetodo { get; set; }
        public virtual MetodosNumericos MetodoNumerico { get; set; }

        [NotMapped] // Para que Entity Framework ignore esta propiedad y no la intente mapear a base de datos
        public virtual List<Iteraciones> ListaIteraciones { get; set; } = new List<Iteraciones>();

    }
}
