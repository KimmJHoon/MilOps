// supabase/functions/accept-invitation/index.ts
// 초대 수락 Edge Function - 회원가입 처리

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type",
};

interface AcceptInvitationRequest {
  invite_code: string;
  login_id: string;
  password: string;
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

    // service_role 클라이언트 (관리자 권한)
    const supabaseAdmin = createClient(supabaseUrl, supabaseServiceKey, {
      auth: {
        autoRefreshToken: false,
        persistSession: false,
      },
    });

    const body: AcceptInvitationRequest = await req.json();
    const { invite_code, login_id, password, email } = body;

    // 1. 입력 검증
    if (!invite_code || !login_id || !password) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "invite_code, login_id, password are required",
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    if (login_id.length < 4 || login_id.length > 50) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "login_id must be 4-50 characters",
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    if (password.length < 8) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "password must be at least 8 characters",
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 2. 초대 정보 조회
    const { data: invitation, error: invError } = await supabaseAdmin
      .from("invitations")
      .select("*")
      .eq("invite_code", invite_code)
      .eq("status", "pending")
      .gt("expires_at", new Date().toISOString())
      .single();

    if (invError || !invitation) {
      return new Response(
        JSON.stringify({
          success: false,
          error: "Invalid or expired invitation code",
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 3. login_id 중복 체크
    const { data: existingUser } = await supabaseAdmin
      .from("users")
      .select("id")
      .eq("login_id", login_id)
      .single();

    if (existingUser) {
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

    // 4. Supabase Auth 사용자 생성
    const userEmail = email || invitation.email || `${login_id}@milops.local`;

    const { data: authData, error: authError } =
      await supabaseAdmin.auth.admin.createUser({
        email: userEmail,
        password: password,
        email_confirm: true, // 이메일 확인 건너뛰기
        user_metadata: {
          login_id: login_id,
          name: invitation.name,
          role: invitation.role,
          invited_via: invite_code,
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

    // 5. public.users 테이블에 프로필 생성
    const { error: userError } = await supabaseAdmin.from("users").insert({
      id: authData.user.id,
      login_id: login_id,
      name: invitation.name,
      phone: invitation.phone,
      email: userEmail,
      role: invitation.role,
      region_id: invitation.region_id,
      district_id: invitation.district_id,
      division_id: invitation.division_id,
      battalion_id: invitation.battalion_id,
      military_rank: invitation.military_rank,
      department: invitation.department,
      position: invitation.position,
      parent_id: invitation.invited_by,
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

    // 6. 초대 상태 업데이트
    console.log(`Updating invitation ${invitation.id} to accepted status`);
    const { data: updatedInv, error: invUpdateError } = await supabaseAdmin
      .from("invitations")
      .update({
        status: "accepted",
        accepted_user_id: authData.user.id,
        accepted_at: new Date().toISOString(),
      })
      .eq("id", invitation.id)
      .select();

    if (invUpdateError) {
      console.error("Invitation update error:", invUpdateError);
      console.error("Invitation ID:", invitation.id);
      // 이미 사용자는 생성됨, 에러 로그만 남김
    } else {
      console.log("Invitation updated successfully:", updatedInv);
    }

    // 7. 알림 생성 (초대자에게)
    await supabaseAdmin.from("notifications").insert({
      user_id: invitation.invited_by,
      type: "invitation_received",
      title: "초대 수락 완료",
      body: `${invitation.name}님이 초대를 수락했습니다.`,
      idempotency_key: `inv_accepted_${invitation.id}`,
    });

    // 8. 성공 응답
    return new Response(
      JSON.stringify({
        success: true,
        user_id: authData.user.id,
        login_id: login_id,
        role: invitation.role,
        message: "Registration completed successfully",
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
