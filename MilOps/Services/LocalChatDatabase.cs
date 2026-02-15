using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MilOps.Models;
using SQLite;

namespace MilOps.Services;

/// <summary>
/// SQLite 로컬 DB — 채팅 기록 캐시 + 로컬 큐 (서버 다운 시 미전송 메시지)
/// SessionStorageService.cs의 파일 경로 패턴 재사용
/// </summary>
public static class LocalChatDatabase
{
    private static SQLiteAsyncConnection? _db;

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MilOps", "messenger.db");

    /// <summary>
    /// SQLite DB 초기화 — 앱 시작 시 1회 호출
    /// </summary>
    public static async Task InitializeAsync()
    {
        if (_db != null) return;

        var dir = Path.GetDirectoryName(DbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _db = new SQLiteAsyncConnection(DbPath);
        await _db.CreateTableAsync<LocalChatMessage>();
        await _db.CreateTableAsync<PendingMessage>();
    }

    // ========== 채팅 기록 캐시 ==========

    /// <summary>
    /// 메시지 목록을 로컬 캐시에 저장 (upsert)
    /// </summary>
    public static async Task SaveMessagesAsync(IEnumerable<LocalChatMessage> messages)
    {
        if (_db == null) return;
        foreach (var msg in messages)
        {
            await _db.InsertOrReplaceAsync(msg);
        }
    }

    /// <summary>
    /// 특정 상대방과의 대화 기록을 로컬 캐시에서 로드
    /// </summary>
    public static async Task<List<LocalChatMessage>> GetMessagesAsync(string partnerId, int limit = 50)
    {
        if (_db == null) return new List<LocalChatMessage>();

        return await _db.QueryAsync<LocalChatMessage>(
            @"SELECT * FROM cached_messages
              WHERE (SenderId = ? OR ReceiverId = ?)
              ORDER BY CreatedAt DESC LIMIT ?",
            partnerId, partnerId, limit);
    }

    /// <summary>
    /// 로컬 캐시 전체 삭제 (로그아웃 시)
    /// </summary>
    public static async Task ClearCacheAsync()
    {
        if (_db == null) return;
        await _db.DeleteAllAsync<LocalChatMessage>();
    }

    // ========== 로컬 큐 (서버 다운 시 미전송 메시지) ==========

    /// <summary>
    /// 미전송 메시지를 큐에 추가
    /// </summary>
    public static async Task EnqueuePendingAsync(string receiverId, string content)
    {
        if (_db == null) return;

        var pending = new PendingMessage
        {
            ReceiverId = receiverId,
            Content = content,
            QueuedAt = DateTime.UtcNow
        };
        await _db.InsertAsync(pending);
    }

    /// <summary>
    /// 큐에 있는 모든 미전송 메시지를 순서대로 반환
    /// </summary>
    public static async Task<List<PendingMessage>> DequeuePendingAsync()
    {
        if (_db == null) return new List<PendingMessage>();

        return await _db.Table<PendingMessage>()
            .OrderBy(m => m.QueueOrder)
            .ToListAsync();
    }

    /// <summary>
    /// 전송 완료된 메시지를 큐에서 제거
    /// </summary>
    public static async Task RemovePendingAsync(int queueOrder)
    {
        if (_db == null) return;
        await _db.DeleteAsync<PendingMessage>(queueOrder);
    }

    /// <summary>
    /// 큐에 대기 중인 메시지 수 반환
    /// </summary>
    public static async Task<int> GetPendingCountAsync()
    {
        if (_db == null) return 0;
        return await _db.Table<PendingMessage>().CountAsync();
    }
}
