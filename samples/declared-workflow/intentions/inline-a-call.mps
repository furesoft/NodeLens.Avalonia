<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="9f8e7d6c-5b4a-4392-9b1a-2d3e4f5a6b71" name="inline-a-call">
  <nodes>
    <node id="2a3b4c5d-6e7f-48a1-9b2c-3d4e5f6a7b82" concept="Intent.Rule" name="Inline this call">
      <properties>
        <conceptKey>Workflow.Call</conceptKey>
        <label>Inline this call</label>
        <description>Puts a copy of the step it calls here, so this step runs those commands itself.</description>
      </properties>
      <children role="action">
        <node id="3b4c5d6e-7f8a-49b2-8c3d-4e5f6a7b8c93" concept="Intent.Inline">
          <properties>
            <reference>calls</reference>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>