using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MilOps.Server;

public class ConnectionManager
{
    private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new();

    public bool TryAdd(Guid userId, ClientSession session)
    {
        // 동일 유저 재접속 시 기존 세션 해제
        if (_sessions.TryRemove(userId, out var oldSession))
        {
            oldSession.Disconnect("New session started");
        }
        return _sessions.TryAdd(userId, session);
    }

    public void Remove(Guid userId)
    {
        _sessions.TryRemove(userId, out _);
    }

    public ClientSession? GetSession(Guid userId)
    {
        _sessions.TryGetValue(userId, out var session);
        return session;
    }

    public bool IsOnline(Guid userId) => _sessions.ContainsKey(userId);

    public int OnlineCount => _sessions.Count;

    public IEnumerable<Guid> OnlineUserIds => _sessions.Keys.ToList();
}
