using EventGraph;
using GraphProcessor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[NodeCustomEditor(typeof(DialogNode))]
public class DialogNodeView : BaseNodeView 
{
    Label printLabel;
    DialogNode dialogNode;

    public override void Enable()
    {
        base.Enable();
        dialogNode = nodeTarget as DialogNode;

        printLabel = new Label();
        controlsContainer.Add(printLabel);

        printLabel.text = string.Format("   {0}\n~ {1}", dialogNode.PreviewParagraphFirst, dialogNode.PreviewParagraphLast);
    }

    public override void OnSelected()
    {
        base.OnSelected();

        printLabel.text = string.Format("   {0}\n~ {1}", dialogNode.PreviewParagraphFirst, dialogNode.PreviewParagraphLast);

    }
}
