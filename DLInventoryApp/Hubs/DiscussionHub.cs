using DLInventoryApp.Data;
using DLInventoryApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Hubs
{
    public class DiscussionHub : Hub
    {
        public Task JoinInventory(string inventoryId)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, $"inventory-{inventoryId}");
        }
    }
}