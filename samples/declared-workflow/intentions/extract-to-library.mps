<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="6f7e8d9c-4b3a-4270-9e1f-3a4b5c6d7e81" name="extract-to-library">
  <nodes>
    <node id="7a8b9c0d-5e4f-4381-af20-4b5c6d7e8f92" concept="Intent.Rule" name="Extract to the library">
      <properties>
        <conceptKey>Workflow.Step</conceptKey>
        <label>Extract to the library</label>
        <description>Moves this step into the library and leaves a call to it here, so the workflow can reuse it.</description>
      </properties>
      <children role="action">
        <node id="8b9c0d1e-6f5a-4492-b031-5c6d7e8f9a03" concept="Intent.Extract">
          <properties>
            <into>Workflow.Workflow</into>
            <slot>library</slot>
            <as>Workflow.Call</as>
            <reference>calls</reference>
            <name>name</name>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>