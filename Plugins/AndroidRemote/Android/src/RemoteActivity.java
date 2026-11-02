package dk.thebonden.controlhub.remote;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.text.InputFilter;
import android.text.InputType;
import android.view.*;
import android.widget.*;
import java.io.InputStream;
import java.util.*;
import java.util.concurrent.*;
import org.json.*;
import javax.net.ssl.SSLException;

public final class RemoteActivity extends Activity {
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private final ArrayList<Button> buttons = new ArrayList<>();
    private SessionStore store;
    private JSONObject session;
    private LinearLayout panel;
    private TextView feedback;
    private EditText invitation, scene;
    private boolean danish, busy;
    private String message = "";
    private String t(String da,String en) { return danish ? da : en; }

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_SECURE);
        danish = getPreferences(MODE_PRIVATE).getBoolean("danish", "da".equals(Locale.getDefault().getLanguage()));
        store = new SessionStore(this);
        try { session = store.load(); }
        catch (Exception error) { store.clear(); message = t("Opret en ny parring.","Pair again."); }
        render();
    }
    private int dp(int n) { return Math.round(getResources().getDisplayMetrics().density*n); }
    private TextView label(String value,int size) {
        TextView text = new TextView(this); text.setText(value); text.setTextSize(size);
        text.setPadding(0,dp(8),0,dp(6)); panel.addView(text); return text;
    }
    private EditText field(String tag,String hint,int max) {
        EditText field = new EditText(this); field.setTag(tag); field.setHint(hint); field.setSingleLine(true);
        field.setFilters(new InputFilter[]{new InputFilter.LengthFilter(max)}); field.setSaveEnabled(false);
        field.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO); panel.addView(field); return field;
    }
    private Button button(String tag,String title,Runnable action) {
        Button button = new Button(this); button.setText(title); button.setAllCaps(false); button.setTag(tag);
        button.setOnClickListener(v -> { if (!busy) action.run(); }); buttons.add(button); panel.addView(button); return button;
    }
    private void render() {
        String previousScene = scene == null ? "" : scene.getText().toString();
        scene = null; invitation = null; buttons.clear();
        ScrollView scroll = new ScrollView(this); scroll.setFillViewport(true);
        panel = new LinearLayout(this); panel.setOrientation(LinearLayout.VERTICAL);
        panel.setPadding(dp(20),dp(30),dp(20),dp(24)); panel.setFocusableInTouchMode(true);
        scroll.addView(panel); setContentView(scroll);
        scroll.setOnApplyWindowInsetsListener((view,insets) -> {
            view.setPadding(0,insets.getSystemWindowInsetTop(),0,insets.getSystemWindowInsetBottom()); return insets;
        });
        label("Control Hub · AndroidRemote",24);
        button("language",danish ? "English" : "Dansk",() -> {
            danish = !danish; getPreferences(MODE_PRIVATE).edit().putBoolean("danish",danish).apply();
            message = ""; render();
        });
        if (session == null) {
            label(t("Par med Control Hub","Pair with Control Hub"),20);
            label(t("Åbn parringsfilen fra Windows, eller indsæt det kopierede bevis. Begge enheder skal være på samme lokale netværk.",
                    "Open the invitation file from Windows or paste the copied invitation. Both devices must be on the same local network."),16);
            invitation = field("invitation",t("Parringsbevis","Invitation"),4096);
            invitation.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
            button("open",t("Åbn parringsfil","Open invitation file"),() -> {
                Intent select = new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("*/*").addCategory(Intent.CATEGORY_OPENABLE);
                startActivityForResult(select,7);
            });
            button("pair",t("Par telefon","Pair phone"),() -> pair(invitation.getText().toString()));
        } else {
            label(session.optString("name","Control Hub"),20);
            if (allowed("powerpoint.control")) {
                button("next",t("Næste slide","Next slide"),() -> command("powerpoint","next",new JSONObject()));
                button("previous",t("Forrige slide","Previous slide"),() -> command("powerpoint","previous",new JSONObject()));
            }
            if (allowed("obs.scenes")) {
                scene = field("scene",t("Præcist navn på OBS-scene","Exact OBS scene name"),256); scene.setText(previousScene);
                button("switch",t("Skift OBS-scene","Switch OBS scene"),() -> {
                    String value = scene.getText().toString();
                    if (value.trim().isEmpty()) { show(t("Indtast scenens navn.","Enter the scene name.")); return; }
                    try { command("obs","scene.switch",new JSONObject().put("sceneName",value)); }
                    catch (JSONException e) { show(t("Ugyldigt navn.","Invalid name.")); }
                });
            }
            if (allowed("status.read")) button("status",t("Hent status","Read status"),this::status);
            button("forget",t("Glem parring","Forget pairing"),() -> {
                store.clear(); session = null; message=t("Parring fjernet fra telefonen.","Pairing removed from the phone."); render();
            });
        }
        feedback = label(message,17); feedback.setTag("feedback"); feedback.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
    }
    private boolean allowed(String scope) {
        JSONArray permissions = session.optJSONArray("permissions");
        for (int i=0; permissions!=null && i<permissions.length(); i++) if (scope.equals(permissions.optString(i))) return true;
        return false;
    }
    private void show(String value) { message=value; feedback.setText(value); }
    private void pair(String text) {
        run(() -> {
            JSONObject offer = new JSONObject(text);
            JSONObject transport = offer.getJSONObject("transport");
            JSONObject proof = new JSONObject().put("id",offer.getString("id")).put("secret",offer.getString("secret"));
            JSONObject credential = new PinnedChannel(transport).request("pair",proof,null);
            JSONObject device = credential.getJSONObject("device");
            JSONObject saved = new JSONObject().put("transport",transport).put("id",device.getString("id"))
                    .put("name",device.getString("name")).put("permissions",device.getJSONArray("permissions"))
                    .put("token",credential.getString("token"));
            store.save(saved);
            return () -> { session=saved; message=t("Telefonen er parret.","Phone paired."); render(); };
        },false);
    }
    @Override protected void onActivityResult(int request,int result,Intent data) {
        super.onActivityResult(request,result,data);
        if (request!=7 || result!=RESULT_OK || data==null || data.getData()==null || session!=null || busy) return;
        android.net.Uri uri = data.getData();
        if (!"content".equals(uri.getScheme())) return;
        run(() -> {
            String contents;
            try (InputStream input=getContentResolver().openInputStream(uri)) { contents=PinnedChannel.read(input,4096); }
            return () -> { invitation.setText(contents); show(t("Bevis indlæst. Tryk Par telefon.","Invitation loaded. Tap Pair phone.")); };
        },false);
    }
    private void command(String plugin,String action,JSONObject arguments) {
        final JSONObject current=session;
        run(() -> {
            String id=UUID.randomUUID().toString();
            JSONObject cmd=new JSONObject().put("protocolVersion","1").put("id",id).put("plugin",plugin)
                    .put("action",action).put("arguments",arguments);
            JSONObject result=new PinnedChannel(current.getJSONObject("transport")).request("commands",cmd,current);
            if (!id.equals(result.getString("id")) || !"1".equals(result.getString("protocolVersion")) ||
                    !plugin.equals(result.getString("plugin"))) throw new java.io.IOException("correlation");
            boolean ok=result.getBoolean("success");
            String detail=result.getJSONObject("message").getString(danish ? "da" : "en");
            return () -> show((ok ? t("Bekræftet. ","Confirmed. ") : "") + detail);
        },true);
    }
    private void status() {
        final JSONObject current=session;
        run(() -> {
            JSONObject answer=new PinnedChannel(current.getJSONObject("transport")).request("status",null,current);
            if (!answer.getBoolean("success")) throw new java.io.IOException("status");
            JSONObject status=answer.getJSONObject("status");
            if (!"1".equals(status.getString("protocolVersion"))) throw new java.io.IOException("protocol");
            JSONArray plugins=status.getJSONArray("plugins");
            StringBuilder lines=new StringBuilder();
            for (int i=0; i<plugins.length(); i++) {
                JSONObject plugin=plugins.getJSONObject(i);
                String id=plugin.getString("id");
                if (!id.equals("obs") && !id.equals("powerpoint")) continue;
                lines.append(id.equals("obs") ? "OBS · " : "PowerPoint · ");
                JSONObject data=plugin.optJSONObject("data");
                if ("ready".equals(plugin.optString("state")) && data!=null && id.equals("powerpoint"))
                    lines.append(t("Slide ","Slide ")).append(data.optString("position","?")).append(" / ").append(data.optString("slideCount","?"));
                else if ("ready".equals(plugin.optString("state")) && data!=null && id.equals("obs") && data.optJSONObject("scene")!=null)
                    lines.append(data.getJSONObject("scene").optString("currentProgramSceneName","?"));
                else lines.append(plugin.getJSONObject("message").getString(danish ? "da":"en"));
                lines.append('\n');
            }
            return () -> show(lines.toString().trim());
        },false);
    }
    private interface Work { Runnable execute() throws Exception; }
    private void run(Work work,boolean command) {
        if (busy) return;
        busy=true; for (Button b:buttons) b.setEnabled(false);
        show(t("Arbejder…","Working…"));
        worker.execute(() -> {
            Runnable finish;
            try { finish=work.execute(); }
            catch (PinnedChannel.Denied denied) {
                finish=() -> {
                    if (denied.status==401 && session!=null) {
                        store.clear(); session=null; message=t("Adgang udløbet eller tilbagekaldt. Par igen.","Access expired or revoked. Pair again."); render();
                    } else if (denied.status==401) show(t("Beviset er udløbet eller brugt. Opret et nyt.","Invitation expired or used. Create a new one."));
                    else if (denied.status==403) show(t("Telefonen mangler denne rettighed.","This phone lacks this permission."));
                    else if (denied.status==429) show(t("Control Hub er optaget. Vent et øjeblik.","Control Hub is busy. Wait a moment."));
                    else show(t("Uventet svar. Kontrollér status.","Unexpected response. Check status."));
                };
            } catch (SSLException error) { finish=() -> show(t("Den sikre forbindelse fejlede. Kontrollér bevis og server.","Secure connection failed. Check the invitation and server.")); }
            catch (Exception error) { finish=() -> show(command ? t("Ukendt udfald. Kontrollér status før næste tryk.","Outcome unknown. Check status before pressing again.")
                    : t("Kontrollér netværk og parringsbevis.","Check the network and invitation.")); }
            final Runnable result=finish;
            runOnUiThread(() -> { if (isFinishing() || isDestroyed()) return; busy=false; result.run(); for (Button b:buttons) b.setEnabled(true); });
        });
    }
    @Override protected void onDestroy() { worker.shutdownNow(); super.onDestroy(); }
}
