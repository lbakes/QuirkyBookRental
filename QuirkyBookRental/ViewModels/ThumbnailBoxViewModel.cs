using QuirkyBookRental.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QuirkyBookRental.ViewModels
{
    public class ThumbnailBoxViewModel
    {
        public IEnumerable<ThumbnailModel>  Thumbnails { get; set; }
    }
}