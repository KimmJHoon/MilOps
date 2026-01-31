import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type",
};

interface NotificationPayload {
  user_id: string;
  title: string;
  body: string;
  type?: string;
  schedule_id?: string;
  data?: Record<string, string>;
}

interface FCMMessage {
  message: {
    token: string;
    notification: {
      title: string;
      body: string;
    };
    data?: Record<string, string>;
    android?: {
      priority: string;
      notification: {
        channel_id: string;
        click_action: string;
      };
    };
  };
}

// Firebase 서비스 계정에서 JWT 토큰 생성
async function getAccessToken(serviceAccount: any): Promise<string> {
  const now = Math.floor(Date.now() / 1000);
  const exp = now + 3600; // 1시간 후 만료

  const header = {
    alg: "RS256",
    typ: "JWT",
  };

  const payload = {
    iss: serviceAccount.client_email,
    sub: serviceAccount.client_email,
    aud: "https://oauth2.googleapis.com/token",
    iat: now,
    exp: exp,
    scope: "https://www.googleapis.com/auth/firebase.messaging",
  };

  // Base64URL 인코딩
  const encoder = new TextEncoder();
  const headerB64 = btoa(JSON.stringify(header))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=/g, "");
  const payloadB64 = btoa(JSON.stringify(payload))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=/g, "");

  const signatureInput = `${headerB64}.${payloadB64}`;

  // RSA 서명 생성
  const privateKeyPem = serviceAccount.private_key;
  const pemContents = privateKeyPem
    .replace("-----BEGIN PRIVATE KEY-----", "")
    .replace("-----END PRIVATE KEY-----", "")
    .replace(/\s/g, "");

  const binaryKey = Uint8Array.from(atob(pemContents), (c) => c.charCodeAt(0));

  const cryptoKey = await crypto.subtle.importKey(
    "pkcs8",
    binaryKey,
    { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
    false,
    ["sign"]
  );

  const signature = await crypto.subtle.sign(
    "RSASSA-PKCS1-v1_5",
    cryptoKey,
    encoder.encode(signatureInput)
  );

  const signatureB64 = btoa(String.fromCharCode(...new Uint8Array(signature)))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=/g, "");

  const jwt = `${signatureInput}.${signatureB64}`;

  // JWT로 액세스 토큰 교환
  const tokenResponse = await fetch("https://oauth2.googleapis.com/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: `grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion=${jwt}`,
  });

  const tokenData = await tokenResponse.json();
  return tokenData.access_token;
}

// FCM 메시지 전송
async function sendFCM(
  accessToken: string,
  projectId: string,
  fcmToken: string,
  title: string,
  body: string,
  data?: Record<string, string>
): Promise<boolean> {
  const message: FCMMessage = {
    message: {
      token: fcmToken,
      notification: {
        title,
        body,
      },
      data: data || {},
      android: {
        priority: "high",
        notification: {
          channel_id: "milops_default_channel",
          click_action: "OPEN_ACTIVITY",
        },
      },
    },
  };

  const response = await fetch(
    `https://fcm.googleapis.com/v1/projects/${projectId}/messages:send`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(message),
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    console.error(`FCM send failed: ${response.status} - ${errorText}`);
    return false;
  }

  console.log(`FCM sent successfully to token: ${fcmToken.substring(0, 20)}...`);
  return true;
}

Deno.serve(async (req) => {
  // CORS 처리
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    // 환경 변수에서 설정 로드
    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
    const firebaseServiceAccount = JSON.parse(
      Deno.env.get("FIREBASE_SERVICE_ACCOUNT") || "{}"
    );

    if (!firebaseServiceAccount.project_id) {
      throw new Error("FIREBASE_SERVICE_ACCOUNT not configured");
    }

    // Supabase 클라이언트 생성 (service role)
    const supabase = createClient(supabaseUrl, supabaseServiceKey);

    // 요청 본문 파싱
    const payload: NotificationPayload = await req.json();
    console.log(`Processing notification for user: ${payload.user_id}`);

    // 사용자의 활성 디바이스 조회
    const { data: devices, error: deviceError } = await supabase
      .from("user_devices")
      .select("fcm_token")
      .eq("user_id", payload.user_id)
      .eq("is_active", true);

    if (deviceError) {
      throw new Error(`Failed to fetch devices: ${deviceError.message}`);
    }

    if (!devices || devices.length === 0) {
      console.log(`No active devices found for user: ${payload.user_id}`);
      return new Response(
        JSON.stringify({ success: true, sent: 0, message: "No devices found" }),
        { headers: { ...corsHeaders, "Content-Type": "application/json" } }
      );
    }

    console.log(`Found ${devices.length} device(s) for user`);

    // Firebase 액세스 토큰 획득
    const accessToken = await getAccessToken(firebaseServiceAccount);

    // 각 디바이스에 FCM 전송
    let sentCount = 0;
    const fcmData: Record<string, string> = {
      type: payload.type || "notification",
      title: payload.title,
      body: payload.body,
      ...(payload.schedule_id && { schedule_id: payload.schedule_id }),
      ...(payload.data || {}),
    };

    for (const device of devices) {
      const success = await sendFCM(
        accessToken,
        firebaseServiceAccount.project_id,
        device.fcm_token,
        payload.title,
        payload.body,
        fcmData
      );

      if (success) {
        sentCount++;
      } else {
        // 실패한 토큰은 비활성화 (선택적)
        await supabase
          .from("user_devices")
          .update({ is_active: false })
          .eq("fcm_token", device.fcm_token);
      }
    }

    return new Response(
      JSON.stringify({
        success: true,
        sent: sentCount,
        total: devices.length,
      }),
      { headers: { ...corsHeaders, "Content-Type": "application/json" } }
    );
  } catch (error) {
    console.error("Error:", error.message);
    return new Response(
      JSON.stringify({ success: false, error: error.message }),
      {
        status: 500,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      }
    );
  }
});
