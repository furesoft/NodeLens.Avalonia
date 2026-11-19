<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="3115e24c-baf3-4d28-b2d9-acd320374d7f" name="mark-as-test">
  <nodes>
    <node id="d2f31ceb-85f6-4e75-8bc7-5ed5fd3ff90f" concept="Intent.Rule" name="Mark as a test">
      <properties>
        <conceptKey>Workflow.Step</conceptKey>
        <label>Mark as a test</label>
        <description>Writes the command this step runs.</description>
      </properties>
      <children role="action">
        <node id="570f8ed1-4007-480d-8bdc-b2eb92c4c733" concept="Intent.Set">
          <properties>
            <slot>command</slot>
            <value>dotnet test</value>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>