// supabase/functions/create-super-admin/index.ts
// 최종관리자 계정 생성 (최초 1회만 사용)

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type",
};

interface CreateSuperAdminRequest {
  admin_secret: string; // 환경변수로 설정한 비밀키
  login_id: string;
  password: string;
  name: string;
  phone: string;
  role: "super_admin_mois" | "super_admin_army";
  email?: string;
}

serve(async (req) => {
  // CORS preflight
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
    const adminSecret = Deno.env.get("ADMIN_SECRET");

    if (!adminSecret) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "ADMIN_SECRET not configured",
        }),
        {
          status: 500,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    const supabaseAdmin = createClient(supabaseUrl, supabaseServiceKey, {
      auth: {
        autoRefreshToken: false,
        persistSession: false,
      },
    });

    const body: CreateSuperAdminRequest = await req.json();

    // 1. 비밀키 검증
    if (body.admin_secret !== adminSecret) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "Unauthorized: Invalid admin secret",
        }),
        {
          status: 401,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 2. 필수 필드 검증
    if (!body.login_id || !body.password || !body.name || !body.phone || !body.role) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "login_id, password, name, phone, role are required",
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 3. 역할 검증
    if (!["super_admin_mois", "super_admin_army"].includes(body.role)) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "Invalid role. Must be super_admin_mois or super_admin_army",
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 4. 이미 해당 역할의 관리자가 있는지 확인
    const { data: existing } = await supabaseAdmin
      .from("users")
      .select("id, login_id")
      .eq("role", body.role)
      .single();

    if (existing) {
      return new Response(
        JSON.stringify({
          success: false,
          error: `${body.role} already exists with login_id: ${existing.login_id}`,
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 5. login_id 중복 체크
    const { data: existingLoginId } = await supabaseAdmin
      .from("users")
      .select("id")
      .eq("login_id", body.login_id)
      .single();

    if (existingLoginId) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "Login ID already exists",
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 6. Auth 사용자 생성
    const userEmail = body.email || `${body.login_id}@milops.local`;

    const { data: authData, error: authError } =
      await supabaseAdmin.auth.admin.createUser({
        email: userEmail,
        password: body.password,
        email_confirm: true,
        user_metadata: {
          login_id: body.login_id,
          name: body.name,
          role: body.role,
        },
      });

    if (authError || !authData.user) {
      console.error("Auth error:", authError);
      return new Response(
        JSON.stringify({
          success: false,
          error: "Failed to create auth user",
          details: authError?.message,
        }),
        {
          status: 500,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 7. public.users 테이블에 프로필 생성
    const { error: userError } = await supabaseAdmin.from("users").insert({
      id: authData.user.id,
      login_id: body.login_id,
      name: body.name,
      phone: body.phone,
      email: userEmail,
      role: body.role,
      // super_admin은 소속 없음 (CHECK 제약조건)
      region_id: null,
      district_id: null,
      division_id: null,
      battalion_id: null,
      parent_id: null,
    });

    if (userError) {
      // 롤백: auth 사용자 삭제
      await supabaseAdmin.auth.admin.deleteUser(authData.user.id);

      console.error("User insert error:", userError);
      return new Response(
        JSON.stringify({
          success: false,
          error: "Failed to create user profile",
          details: userError.message,
        }),
        {
          status: 500,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 8. 성공 응답
    const roleNames: Record<string, string> = {
      super_admin_mois: "행정안전부 관리자",
      super_admin_army: "육군본부 관리자",
    };

    return new Response(
      JSON.stringify({
        success: true,
        user_id: authData.user.id,
        login_id: body.login_id,
        role: body.role,
        role_name: roleNames[body.role],
        message: "Super admin created successfully",
      }),
      {
        status: 200,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      }
    );
  } catch (error) {
    console.error("Unexpected error:", error);
    return new Response(
      JSON.stringify({
        success: false,
        error: "Internal server error",
      }),
      {
        status: 500,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      }
    );
  }
});
