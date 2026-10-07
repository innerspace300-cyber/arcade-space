using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DynamicAttrBar : MonoBehaviour
{
    [SerializeField]
    private Image dynamicBar;
    [SerializeField]
    private Image blinkBorder;
    [SerializeField]
    private Image portraitBorder;
    [SerializeField]
    private AttrBar attrBar;
    [SerializeField]
    private bool changeSize = true;
    [SerializeField]
    private MinAndMaxValues minAndMax = new MinAndMaxValues(0, 48);
    [SerializeField]
    private float maxSize = 250;
    [SerializeField]
    private float divider = 1;
    [SerializeField]
    private RectTransform hitFXTransform;
    [SerializeField]
    private RectTransform barEdgeTransform;
    [SerializeField]
    private RectTransform barEdgeMask;
    [SerializeField]
    [Range(0, 10)]
    private float minFillValue = 0;
    [SerializeField]
    private Vector2 edgePadding = Vector2.zero;
    [SerializeField]
    private Vector2 edgeFillPadding = Vector2.zero;

    private Vector2 startSize;
    private Vector2 startBGSize;
    private Vector2 startBorderSize;
    private Vector2 startBar2Size;
    private Vector2 startUseBarSize;
    private Vector2 startUsedBarSize;
    private Vector2 startBarEdgeMaskSize;

    private Vector2 startBlinkBorderSize;

    private Animator anim;

    float currentExtraValue = 0;

    // Start is called before the first frame update
    void Start()
    {
        if (!attrBar)
        {
            attrBar = GetComponent<AttrBar>();
        }

        anim = GetComponent<Animator>();

        if (attrBar)
        {
            startSize = dynamicBar.GetComponent<RectTransform>().sizeDelta;
            startBGSize = attrBar.BarBackground.GetComponent<RectTransform>().sizeDelta;
            startBorderSize = attrBar.BarBorder.GetComponent<RectTransform>().sizeDelta;
            startBar2Size = attrBar.SecondaryBar.GetComponent<RectTransform>().sizeDelta;

            if (attrBar.UseBar)
            {
                startUseBarSize = attrBar.UseBar.GetComponent<RectTransform>().sizeDelta;
            }

            if (attrBar.UsedBar)
            {
                startUsedBarSize = attrBar.UsedBar.GetComponent<RectTransform>().sizeDelta;
            }

            if (barEdgeMask)
            {
                startBarEdgeMaskSize = barEdgeMask.sizeDelta;
            }
        }

        if (blinkBorder)
        {
            startBlinkBorderSize = blinkBorder.GetComponent<RectTransform>().sizeDelta;
        }
    }

    // Update is called once per frame
    void Update()
    {
        ChangeSize();
        ChangeEdgePos();
    }

    private void ChangeSize()
    {
        if (!dynamicBar)
        {
            Debug.LogError("No dynamic bar set!");
            return;
        }

        if (!changeSize || !attrBar)
        {
            return;
        }

        Image attrBarImage = attrBar.Bar;
        Image attrBarBG = attrBar.BarBackground;
        Image attrBarBorder = attrBar.BarBorder;
        Image secondaryBar = attrBar.SecondaryBar;
        Image useBar = attrBar.UseBar;
        Image usedBar = attrBar.UsedBar;

        RectTransform dynamicBarTransform = dynamicBar.GetComponent<RectTransform>();

        RectTransform barTransform = null;
        RectTransform barBGTransform = null;
        RectTransform barBorderTransform = null;
        RectTransform bar2Transform = null;
        RectTransform useBarTransform = null;
        RectTransform usedBarTransform = null;

        if (attrBarImage)
        {
            barTransform = attrBarImage.GetComponent<RectTransform>();
        }

        if (attrBarBG)
        {
            barBGTransform = attrBarBG.GetComponent<RectTransform>();
        }

        if (attrBarBorder)
        {
            barBorderTransform = attrBarBorder.GetComponent<RectTransform>();
        }

        if (secondaryBar)
        {
            bar2Transform = secondaryBar.GetComponent<RectTransform>();
        }

        if (useBar)
        {
            useBarTransform = useBar.GetComponent<RectTransform>();
        }

        if (usedBar)
        {
            usedBarTransform = usedBar.GetComponent<RectTransform>();
        }

        Vector2 newSize = startSize;

        float maxAttrValue = attrBar.GetMaxAttrByAttrType(attrBar.AttrType1);

        float extraProportion = (maxAttrValue / minAndMax.Max) / divider;

        newSize.x *= extraProportion;

        newSize.x = Mathf.Clamp(newSize.x, startSize.x, startSize.x + maxSize);

        newSize.x = Mathf.FloorToInt(newSize.x);

        if (extraProportion != currentExtraValue)
        {
            if (extraProportion > 1)
            {
                Transform barMask = dynamicBarTransform.parent;

                Vector2 pos = dynamicBarTransform.localPosition;

                dynamicBarTransform.SetParent(null);

                dynamicBarTransform.sizeDelta = newSize;

                float extraWidth = newSize.x - startSize.x;

                if (attrBarImage)
                {
                    barTransform.sizeDelta = newSize;
                }

                ChangeSize(barBGTransform, startBGSize, extraWidth);

                ChangeSize(barBorderTransform, startBorderSize, extraWidth);

                ChangeSize(bar2Transform, startBar2Size, extraWidth);

                ChangeSize(useBarTransform, startUseBarSize, extraWidth);

                ChangeSize(usedBarTransform, startUsedBarSize, extraWidth);

                ChangeSize(barEdgeMask, startBarEdgeMaskSize, extraWidth);

                if (blinkBorder)
                {
                    ChangeSize(blinkBorder.GetComponent<RectTransform>(), startBlinkBorderSize, extraWidth);
                }

                dynamicBarTransform.SetParent(barMask);

                dynamicBarTransform.localPosition = pos;
            }
        }

        currentExtraValue = extraProportion;
    }

    private void ChangeEdgePos()
    {
        if (!barEdgeTransform)
        {
            return;
        }

        Image attrBarImage = attrBar.Bar;
        RectTransform barTransform = null;

        if (attrBarImage)
        {
            barTransform = attrBarImage.GetComponent<RectTransform>();
        }

        float maxAttr = attrBar.GetMaxAttrByAttrType(attrBar.AttrType1);

        float value = attrBar.GetAttrValueByAttrType(attrBar.AttrType1);

        float barWidth = barTransform ? barTransform.sizeDelta.x : 0;

        float fillAmount = attrBarImage ? attrBarImage.fillAmount : 0;

        bool filled = maxAttr - value <= minFillValue;

        if (barEdgeTransform)
        {
            Vector2 localPos = barEdgeTransform.localPosition;

            localPos.x = (barWidth * (!filled ? fillAmount : 1));

            barEdgeTransform.localPosition = localPos - (!filled ? edgePadding : edgeFillPadding);
        }
    }


    private void ChangeSize(RectTransform rectTransform, Vector2 startSize, float extraWidth)
    {
        if (rectTransform)
        {
            float xSize = startSize.x + extraWidth;

            Vector2 newSize = new Vector2(xSize, rectTransform.sizeDelta.y);

            rectTransform.sizeDelta = newSize;
        }
    }

    private void GotHit()
    {
        if (anim)
        {
            anim.Play("OnHit");
        }

        Image attrBarImage = attrBar.Bar;
        RectTransform barTransform = null;

        if (attrBarImage)
        {
            barTransform = attrBarImage.GetComponent<RectTransform>();
        }

        float barWidth = barTransform ? barTransform.sizeDelta.x : 0;

        float fillAmount = attrBarImage ? attrBarImage.fillAmount : 0;

        if (hitFXTransform && attrBarImage)
        {
            Vector2 localPos = hitFXTransform.localPosition;

            localPos.x = (barWidth * fillAmount);

            hitFXTransform.localPosition = localPos;
        }

        if (barEdgeTransform)
        {
            Animator edgeAnim = barEdgeTransform.GetComponent<Animator>();

            if (edgeAnim)
            {
                //Debug.Log("Playing OnHit animation");

                edgeAnim.Play("OnHit");
            }
        }
    }

    [System.Serializable]
    public struct MinAndMaxValues
    {
        [SerializeField]
        float min;
        [SerializeField]
        float max;

        public MinAndMaxValues(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Min { get => min; set => min = value; }
        public float Max { get => max; set => max = value; }
    }
}
