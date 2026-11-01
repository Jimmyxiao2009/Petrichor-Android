namespace Petrichor.Mobile.UI;

internal static class MaterialMotion
{
    // Material standard easing: cubic-bezier(.2, 0, 0, 1).
    public static readonly Easing Standard=new(t=>{
        var low=0d;var high=1d;
        for(var i=0;i<14;i++){var u=(low+high)/2;var x=3*(1-u)*(1-u)*u*.2+u*u*u;if(x<t)low=u;else high=u;}
        var v=(low+high)/2;return 3*(1-v)*v*v+v*v*v;
    });
}
