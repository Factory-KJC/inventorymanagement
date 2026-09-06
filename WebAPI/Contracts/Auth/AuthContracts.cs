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

public sealed record LoginResponse(string Token, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>
/// 使用済みトークンを無効化し、新しいトークン組へ交換する要求です。
/// </summary>
public sealed record RefreshTokenRequest([Required] string RefreshToken);

public sealed record MessageResponse(string Message);
