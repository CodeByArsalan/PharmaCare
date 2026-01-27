using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PharmaCare.Domain.ViewModels;

public class LoginUserViewModel
{
    public int UserID { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string UserName { get; set; }
    public int LoginUserTypeID { get; set; }
    public string LoginUserType { get; set; }
    public int? Store_ID { get; set; }
}
