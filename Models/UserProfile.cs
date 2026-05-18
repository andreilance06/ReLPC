using System;
using System.Collections.Generic;

namespace ReLPC.Models;

public class UserProfile
{
    // LiteDB uses this as the Primary Key
    public int Id { get; set; }

    // --- Login Info ---
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // --- History Info (Bundled/Embedded) ---
    // Using a List here allows LiteDB to save all history 
    // inside the same JSON-like document as the user.
    public List<List<Point>> Datasets { get; set; } = [];
}