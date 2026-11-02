package dk.thebonden.controlhub.remote.test;

import android.app.*;
import android.content.*;
import android.os.Bundle;
import android.util.Base64;
import android.view.View;
import android.widget.*;
import java.util.concurrent.atomic.AtomicReference;
import org.json.JSONObject;

public final class PhoneTrial extends Instrumentation {
    private Bundle args;
    private Activity app;
    private String step;
    @Override public void onCreate(Bundle args) { this.args=args; start(); }
    @Override public void onStart() {
        Bundle result=new Bundle();
        try {
            Intent start=new Intent().setClassName("dk.thebonden.controlhub.remote","dk.thebonden.controlhub.remote.RemoteActivity");
            start.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
            app=startActivitySync(start);
            if ("offline".equals(args.getString("phase"))) {
                press("next"); expect("Outcome unknown");
            } else if ("restored".equals(args.getString("phase"))) {
                press("status"); expect("Slide 1 / 4"); expect("OBS · Camera");
            } else if ("revoked".equals(args.getString("phase"))) {
                press("status"); expect("Access expired or revoked"); exists("pair");
            } else {
                if ("English".equals(text("language"))) press("language");
                String invitation=new String(Base64.decode(args.getString("invite"),Base64.DEFAULT),"UTF-8");
                JSONObject bad=new JSONObject(invitation);
                bad.getJSONObject("transport").put("certificateSha256",new String(new char[64]).replace('\0','0'));
                enter("invitation",bad.toString()); press("pair"); expect("Secure connection failed");
                enter("invitation",invitation); press("pair"); expect("Phone paired");
                press("next"); expect("Confirmed."); press("status"); expect("Slide 2 / 4");
                press("previous"); expect("Confirmed."); enter("scene","Camera"); press("switch"); expect("Confirmed.");
                press("status"); expect("OBS · Camera"); expect("Slide 1 / 4");
            }
            result.putString("result","PASS");
            finish(Activity.RESULT_OK,result);
        } catch (Throwable error) {
            result.putString("result","FAIL at " + step + " (" + error.getClass().getSimpleName() + ")");
            finish(Activity.RESULT_CANCELED,result);
        }
    }
    private View find(String tag) { return app.getWindow().getDecorView().findViewWithTag(tag); }
    private void exists(String tag) {
        final AtomicReference<View> found=new AtomicReference<>();
        runOnMainSync(() -> found.set(find(tag)));
        if (found.get()==null) throw new AssertionError();
    }
    private void press(String tag) {
        step="press "+tag;
        runOnMainSync(() -> { View v=find(tag); if(v==null || !v.isEnabled()) return; v.performClick(); });
    }
    private void enter(String tag,String value) {
        step="enter "+tag;
        runOnMainSync(() -> ((EditText)find(tag)).setText(value));
    }
    private String text(String tag) {
        AtomicReference<String> value=new AtomicReference<>();
        runOnMainSync(() -> value.set(((TextView)find(tag)).getText().toString()));
        return value.get();
    }
    private void expect(String value) throws Exception {
        step="expect "+value;
        long end=android.os.SystemClock.elapsedRealtime()+40000;
        while (android.os.SystemClock.elapsedRealtime()<end) {
            if (text("feedback").contains(value)) return;
            Thread.sleep(100);
        }
        throw new AssertionError();
    }
}
