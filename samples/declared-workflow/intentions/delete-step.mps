<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="4f7ede46-914e-465f-a487-3ffcf78d3d88" name="delete-step">
  <nodes>
    <node id="60a5469d-0fc0-46ff-9fdf-fc442f696185" concept="Intent.Rule" name="Delete this step">
      <properties>
        <conceptKey>Workflow.Step</conceptKey>
        <label>Delete this step</label>
        <description>Removes the step, so the one before it runs next.</description>
      </properties>
      <children role="action">
        <node id="61ac01fd-266b-43ab-8ec7-2e4cf8758b2e" concept="Intent.Delete" />
      </children>
    </node>
  </nodes>
</model>