// supabase/functions/validate-invitation/index.ts
// 초대 코드 유효성 검증 (가입 화면 진입 전 확인용)

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type",
};

interface ValidateInvitationRequest {
  invite_code: string;
}

serve(async (req) => {
  // CORS preflight
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

    const supabaseAdmin = createClient(supabaseUrl, supabaseServiceKey, {
      auth: {
        autoRefreshToken: false,
        persistSession: false,
      },
    });

    const body: ValidateInvitationRequest = await req.json();
    const { invite_code } = body;

    if (!invite_code) {
      return new Response(
        JSON.stringify({
          valid: false,
          error: "invite_code is required",
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 초대 정보 조회 (민감한 정보 제외)
    const { data: invitation, error } = await supabaseAdmin
      .from("invitations")
      .select(
        `
        id,
        role,
        name,
        phone,
        expires_at,
        region_id,
        district_id,
        division_id,
        battalion_id,
        regions:region_id(name),
        districts:district_id(name),
        divisions:division_id(name),
        battalions:battalion_id(name)
      `
      )
      .eq("invite_code", invite_code)
      .eq("status", "pending")
      .gt("expires_at", new Date().toISOString())
      .single();

    if (error || !invitation) {
      return new Response(
        JSON.stringify({
          valid: false,
          error: "Invalid or expired invitation code",
        }),
        {
          status: 200, // 200으로 응답 (valid: false로 구분)
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // 소속 정보 문자열 생성
    let affiliation = "";
    const regions = invitation.regions as { name: string } | null;
    const districts = invitation.districts as { name: string } | null;
    const divisions = invitation.divisions as { name: string } | null;
    const battalions = invitation.battalions as { name: string } | null;

    if (regions) {
      affiliation = regions.name;
      if (districts) {
        affiliation += " " + districts.name;
      }
    } else if (divisions) {
      affiliation = divisions.name;
      if (battalions) {
        affiliation += " " + battalions.name;
      }
    }

    // 역할 한글 변환
    const roleNames: Record<string, string> = {
      middle_local: "지자체(도) 중간관리자",
      middle_military: "사단담당자",
      user_local: "지자체담당자",
      user_military: "대대담당자",
    };

    // 남은 시간 계산
    const expiresAt = new Date(invitation.expires_at);
    const now = new Date();
    const remainingHours = Math.floor(
      (expiresAt.getTime() - now.getTime()) / (1000 * 60 * 60)
    );
    const remainingDays = Math.floor(remainingHours / 24);

    let remainingText = "";
    if (remainingDays > 0) {
      remainingText = `${remainingDays}일 ${remainingHours % 24}시간`;
    } else {
      remainingText = `${remainingHours}시간`;
    }

    return new Response(
      JSON.stringify({
        valid: true,
        invitation: {
          role: invitation.role,
          role_name: roleNames[invitation.role] || invitation.role,
          name: invitation.name,
          phone: invitation.phone,
          affiliation: affiliation,
          expires_at: invitation.expires_at,
          remaining_time: remainingText,
        },
      }),
      {
        status: 200,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      }
    );
  } catch (error) {
    console.error("Error:", error);
    return new Response(
      JSON.stringify({
        valid: false,
        error: "Internal server error",
      }),
      {
        status: 500,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      }
    );
  }
});
