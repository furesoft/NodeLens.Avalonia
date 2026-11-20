<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9000-000000000005" name="Decl.If">
  <nodes>
    <node id="d0000000-0000-4000-8000-000000000005" concept="Structure.Concept" name="Decl.If">
      <properties>
        <key>Decl.If</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>One expression chosen by a condition.</description>
        <nameSlot></nameSlot>
        <nameRule>camel</nameRule>
      </properties>
      <children role="supertypes">
        <node id="d0000000-0000-4000-8000-000000000105" concept="Structure.SuperType" name="Decl.Expression">
          <properties>
            <conceptKey>Decl.Expression</conceptKey>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="d0000000-0000-4000-8000-000000000306" concept="Structure.ChildDecl" name="condition">
          <properties>
            <name>condition</name>
            <childConceptKey>Decl.Expression</childConceptKey>
            <cardinality>0..1</cardinality>
          </properties>
        </node>
        <node id="d0000000-0000-4000-8000-000000000307" concept="Structure.ChildDecl" name="then">
          <properties>
            <name>then</name>
            <childConceptKey>Decl.Expression</childConceptKey>
            <cardinality>0..1</cardinality>
          </properties>
        </node>
        <node id="d0000000-0000-4000-8000-000000000308" concept="Structure.ChildDecl" name="else">
          <properties>
            <name>else</name>
            <childConceptKey>Decl.Expression</childConceptKey>
            <cardinality>0..1</cardinality>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>