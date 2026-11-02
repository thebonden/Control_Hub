package dk.thebonden.controlhub.remote;

import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.security.*;
import java.security.cert.*;
import javax.net.ssl.*;
import org.json.*;

final class PinnedChannel {
    static final class Denied extends IOException {
        final int status;
        Denied(int status) { super("HTTP " + status); this.status = status; }
    }
    private final URI endpoint;
    private final SSLSocketFactory sockets;

    PinnedChannel(JSONObject transport) throws Exception {
        endpoint = new URI(transport.getString("endpoint"));
        String host = endpoint.getHost();
        if (!"https".equals(endpoint.getScheme()) || host == null || !privateIPv4(host)
                || endpoint.getPort() < 1 || endpoint.getPort() > 65535 || endpoint.getRawUserInfo() != null
                || endpoint.getRawQuery() != null || endpoint.getRawFragment() != null
                || !(endpoint.getPath().isEmpty() || "/".equals(endpoint.getPath())))
            throw new IllegalArgumentException("endpoint");
        String pin = transport.getString("certificateSha256");
        if (!pin.matches("[0-9A-Fa-f]{64}")) throw new IllegalArgumentException("pin");
        byte[] expected = new byte[32];
        for (int i = 0; i < expected.length; i++) expected[i] = (byte)Integer.parseInt(pin.substring(i*2, i*2+2),16);
        X509TrustManager trust = new X509TrustManager() {
            public X509Certificate[] getAcceptedIssuers() { return new X509Certificate[0]; }
            public void checkClientTrusted(X509Certificate[] chain, String auth) throws CertificateException {
                throw new CertificateException("client");
            }
            public void checkServerTrusted(X509Certificate[] chain, String auth) throws CertificateException {
                if (chain == null || chain.length == 0) throw new CertificateException("empty");
                chain[0].checkValidity();
                try {
                    byte[] actual = MessageDigest.getInstance("SHA-256").digest(chain[0].getEncoded());
                    if (!MessageDigest.isEqual(expected, actual)) throw new CertificateException("identity");
                } catch (NoSuchAlgorithmException e) { throw new CertificateException(e); }
            }
        };
        SSLContext tls = SSLContext.getInstance("TLS");
        tls.init(null,new TrustManager[]{trust},null);
        sockets = tls.getSocketFactory();
    }

    private static boolean privateIPv4(String host) {
        String[] parts = host.split("\\.", -1);
        if (parts.length != 4) return false;
        int[] bytes = new int[4];
        for (int i=0; i<4; i++) {
            if (!parts[i].matches("0|[1-9][0-9]{0,2}")) return false;
            bytes[i] = Integer.parseInt(parts[i]);
            if (bytes[i] > 255) return false;
        }
        return host.equals("127.0.0.1") || bytes[0]==10 || (bytes[0]==172 && bytes[1]>=16 && bytes[1]<=31)
                || (bytes[0]==192 && bytes[1]==168);
    }

    JSONObject request(String route, JSONObject body, JSONObject session) throws Exception {
        URL url = endpoint.resolve("/phone/v1/" + route).toURL();
        HttpsURLConnection connection = (HttpsURLConnection)url.openConnection(Proxy.NO_PROXY);
        try {
            connection.setSSLSocketFactory(sockets);
            // Default hostname verification also checks the IP subject alternative name.
            connection.setInstanceFollowRedirects(false);
            connection.setUseCaches(false);
            connection.setConnectTimeout(6000);
            connection.setReadTimeout(35000);
            if (session != null) {
                connection.setRequestProperty("X-ControlHub-Device", session.getString("id"));
                connection.setRequestProperty("Authorization", "Bearer " + session.getString("token"));
            }
            if (body != null) {
                byte[] bytes = body.toString().getBytes(StandardCharsets.UTF_8);
                connection.setRequestMethod("POST");
                connection.setDoOutput(true);
                connection.setFixedLengthStreamingMode(bytes.length);
                connection.setRequestProperty("Content-Type","application/json");
                try (OutputStream out = connection.getOutputStream()) { out.write(bytes); }
            }
            int status = connection.getResponseCode();
            if (status != 200) throw new Denied(status);
            try (InputStream in = connection.getInputStream()) { return new JSONObject(read(in, 256 * 1024)); }
        } finally { connection.disconnect(); }
    }

    static String read(InputStream in, int limit) throws IOException {
        if (in == null) throw new IOException("missing input");
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buffer = new byte[4096];
        for (int count; (count = in.read(buffer)) != -1;) {
            if (count > limit - out.size()) throw new IOException("size");
            out.write(buffer,0,count);
        }
        return out.toString("UTF-8");
    }
}
