-- ============================================================
-- MilOps 캐시 데이터 일괄 조회 RPC 함수
-- 8개 개별 REST 요청 → 2개 RPC 호출로 통합 (네트워크 왕복 8회 → 2회)
-- Supabase Dashboard > SQL Editor 에서 실행
-- ============================================================

-- 1. OrgCache: 조직 데이터 일괄 조회 (regions, districts, divisions, brigades, battalions)
CREATE OR REPLACE FUNCTION get_org_cache_data()
RETURNS json
LANGUAGE sql
STABLE
SECURITY DEFINER
AS $$
  SELECT json_build_object(
    'regions',    COALESCE((SELECT json_agg(row_to_json(r)) FROM regions r    WHERE r.is_active = true), '[]'::json),
    'districts',  COALESCE((SELECT json_agg(row_to_json(d)) FROM districts d  WHERE d.is_active = true), '[]'::json),
    'divisions',  COALESCE((SELECT json_agg(row_to_json(v)) FROM divisions v), '[]'::json),
    'brigades',   COALESCE((SELECT json_agg(row_to_json(b)) FROM brigades b   WHERE b.is_active = true), '[]'::json),
    'battalions', COALESCE((SELECT json_agg(row_to_json(t)) FROM battalions t WHERE t.is_active = true), '[]'::json)
  );
$$;

-- 2. BizCache: 업무 데이터 일괄 조회 (companies, users, schedule_company_ids)
CREATE OR REPLACE FUNCTION get_biz_cache_data()
RETURNS json
LANGUAGE sql
STABLE
SECURITY DEFINER
AS $$
  SELECT json_build_object(
    'companies',          COALESCE((SELECT json_agg(row_to_json(c)) FROM companies c WHERE c.is_active = true), '[]'::json),
    'users',              COALESCE((SELECT json_agg(row_to_json(u)) FROM users u     WHERE u.is_active = true), '[]'::json),
    'schedule_company_ids', COALESCE((SELECT json_agg(DISTINCT s.company_id) FROM schedules s WHERE s.deleted_at IS NULL), '[]'::json)
  );
$$;

-- 권한 설정: authenticated 사용자만 호출 가능
GRANT EXECUTE ON FUNCTION get_org_cache_data() TO authenticated;
GRANT EXECUTE ON FUNCTION get_biz_cache_data() TO authenticated;

-- anon 사용자에게도 org 데이터 허용 (로그인 전 필요할 수 있음)
GRANT EXECUTE ON FUNCTION get_org_cache_data() TO anon;
