using System.Collections.Specialized;
using System.Net;
using System.ComponentModel;

namespace PantryPal.Models;

public class Favorites: INotifyPropertyChanged
{
    public string FavoriteTitle { get; set; } = "";
    public string FavoriteImageURL { get; set; } = "";
    public int FavoriteReadyInMinutes { get; set; }
    public int FavoriteServings { get; set; }
}
