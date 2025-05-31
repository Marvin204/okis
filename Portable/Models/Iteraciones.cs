using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using Microsoft.Ajax.Utilities;

namespace Portable.Models
{
    public class Iteraciones
    {
        public int IdIteracion { get; set; }

        public int Numero{ get; set; }
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double X2 { get; set; }

        

        public double FX0 { get; set; }
        public double FX1 { get; set; }
        public double? FX2 { get; set; }
        public double Error { get; set; }

        public string MetodoUsado { get; set; }

        // Clave foránea
        [ForeignKey("Resultados")]
        public int IdResultado { get; set; }
        public virtual Resultados Resultado { get; set; }


    }
}