<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="51a4ff13-bf49-47c7-a0ae-37789d15e12f" name="stage-command">
  <nodes>
    <node id="710f5adf-b31e-45e3-be54-5ace6e41a2a1" concept="Constraint.Rule" name="the command starts or ends with whitespace, which reads as part of it.">
      <properties>
        <conceptKey>Pipeline.Stage</conceptKey>
        <message>the command starts or ends with whitespace, which reads as part of it.</message>
        <severity>warning</severity>
        <code>pipeline.stage.command</code>
      </properties>
      <children role="body">
        <node id="33bf6c9f-c6cc-4f10-9da8-5d4d8de924ef" concept="Constraint.SurroundingWhitespace">
          <properties>
            <slot>command</slot>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>