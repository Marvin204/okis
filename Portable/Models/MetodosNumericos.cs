using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Portable.Models
{
  
       [Table("MetodosNumericos")]
        public class MetodosNumericos
    {
        [Key]
        public int IdMetodo { get; set; }

        [Required]
        [StringLength(100)]
        public string NombreMetodo { get; set; }

        [StringLength(255)]
        public string Descripcion { get; set; }
        }
        
}