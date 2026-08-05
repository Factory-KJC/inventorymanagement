using System.ComponentModel.DataAnnotations;

namespace InventoryAPI.Contracts.Auth;

/// <summary>
/// ログイン時に受け取る資格情報です。
/// </summary>
public sealed record LoginRequest(
    [Required] string Username,
    [Required] string Password);

/// <summary>
/// 初回管理ユーザーを登録するための資格情報です。
/// </summary>
public sealed record RegisterUserRequest(
    [Required] string Username,
    [Required] string Password);

public sealed record LoginResponse(string Token);

public sealed record MessageResponse(string Message);
