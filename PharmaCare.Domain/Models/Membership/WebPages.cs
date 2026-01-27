using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PharmaCare.Domain.Models.Membership;

public class WebPages
{
    [Key]
    public int WebPageID { get; set; }
    public int Parent_ID { get; set; }
    public bool IsVisible { get; set; }
    public string? PageIcon { get; set; }
    public int PageOrder { get; set; }
    public string? PageTitle { get; set; }
    public string? ControllerName { get; set; }
    public string? ViewName { get; set; }
    public string? Description { get; set; }
    public ICollection<WebPageUrls>? WebPageUrls { get; set; }
}

public class WebPageUrls
{
    [Key]
    public int WebPageUrlID { get; set; }
    [ForeignKey("WebPage")]
    public int WebPage_ID { get; set; }
    // Navigation property for the foreign key relationship
    public WebPages WebPage { get; set; }
    public string Url { get; set; }
}
