<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f9f69acb-5710-4c10-83e0-1697891c8c2a" name="add-first-step">
  <nodes>
    <node id="39a85629-162f-4b63-8c91-75d912ebde71" concept="Intent.Rule" name="Add the first step">
      <properties>
        <conceptKey>Workflow.Workflow</conceptKey>
        <label>Add the first step</label>
        <description>Puts a step into the empty place of the workflow.</description>
      </properties>
      <children role="action">
        <node id="6cba1288-7f13-4d98-9e92-8c24e964c3d9" concept="Intent.Insert">
          <properties>
            <concept>Workflow.Step</concept>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>