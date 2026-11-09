using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Security;

public static class AdminProvisioner
{
    public static async Task EnsureSuperadminAsync(AppDbContext db,string username,string email,string password,CancellationToken ct=default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Admin username, email, and password are required.");
        var normalizedEmail=email.Trim().ToLowerInvariant();
        var matches=await db.Users.Where(u=>u.Username.ToLower()==username.Trim().ToLower()||u.Email.ToLower()==normalizedEmail).ToListAsync(ct);
        var user=matches.FirstOrDefault();
        if(user is null){user=new User{Username=username.Trim(),Email=normalizedEmail,Name="TAGlobal Superadmin",Country="Indonesia",Role="admin",PasswordHash=BCrypt.Net.BCrypt.HashPassword(password)};db.Users.Add(user);}
        else{user.Username=username.Trim();user.Email=normalizedEmail;user.Name="TAGlobal Superadmin";user.Country="Indonesia";user.Role="admin";user.SellerId=null;user.PasswordHash=BCrypt.Net.BCrypt.HashPassword(password);}
        foreach(var duplicate in matches.Where(x=>x.Id!=user.Id))db.Users.Remove(duplicate);
        await db.SaveChangesAsync(ct);
    }
}

